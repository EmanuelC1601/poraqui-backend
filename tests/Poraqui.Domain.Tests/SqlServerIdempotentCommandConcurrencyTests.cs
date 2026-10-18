using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerIdempotentCommandConcurrencyTests
{
    [SqlServerIntegrationFact]
    public async Task Concurrent_offline_delivery_retries_with_the_same_command_persist_one_delivery_and_replay()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var setup = new AppDbContext(options);
        await setup.Database.MigrateAsync();

        var owner = NewUser("offline-owner");
        var customer = NewUser("offline-customer");
        var reviewer = NewUser("offline-reviewer");
        var courier = NewUser("offline-courier");
        var zone = new DeliveryZone($"Offline retry zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Offline retry business {Guid.NewGuid():N}", "SQL test pickup address");
        var order = CreateInTransitOrder(owner, customer, reviewer, courier, business, zone);
        var orderId = order.Id;
        var commandId = Guid.NewGuid();
        const string deliveryCode = "654321";
        var capturedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-2);
        setup.Users.AddRange(owner, customer, reviewer, courier);
        setup.DeliveryZones.Add(zone);
        setup.Businesses.Add(business);
        setup.Orders.Add(order);
        await setup.SaveChangesAsync();

        try
        {
            var barrier = new TwoPartySaveBarrier();
            async Task<(OfflineDeliverySyncResult? Result, Exception? Error)> AttemptAsync()
            {
                var attemptOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString)
                    .AddInterceptors(barrier).Options;
                await using var db = new AppDbContext(attemptOptions);
                var service = CreateWorkflowService(db);
                try
                {
                    return (await service.SyncDeliveryConfirmationAsync(orderId, courier.Id, commandId, deliveryCode,
                        capturedAtUtc, CancellationToken.None), null);
                }
                catch (Exception exception) { return (null, exception); }
            }

            var attempts = await Task.WhenAll(AttemptAsync(), AttemptAsync());
            Assert.All(attempts, attempt => Assert.Null(attempt.Error));
            Assert.Single(attempts, attempt => attempt.Result is { Replayed: false });
            Assert.Single(attempts, attempt => attempt.Result is { Replayed: true });
            Assert.Equal(attempts[0].Result!.AcceptedAtUtc, attempts[1].Result!.AcceptedAtUtc);

            setup.ChangeTracker.Clear();
            var persistedOrder = await setup.Orders.Include(candidate => candidate.StatusHistory)
                .SingleAsync(candidate => candidate.Id == orderId);
            Assert.Equal(OrderStatus.Delivered, persistedOrder.Status);
            Assert.Single(persistedOrder.StatusHistory, change => change.Status == OrderStatus.Delivered);
            Assert.Single(await setup.OfflineDeliverySyncRecords.AsNoTracking()
                .Where(command => command.ActorUserId == courier.Id && command.Id == commandId).ToArrayAsync());
        }
        finally
        {
            await CleanupAsync(setup, orderId, business.Id, zone.Id, [owner.Id, customer.Id, reviewer.Id, courier.Id]);
        }
    }

    [SqlServerIntegrationFact]
    public async Task Concurrent_incident_retries_with_the_same_command_persist_one_incident_and_return_the_same_result()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var setup = new AppDbContext(options);
        await setup.Database.MigrateAsync();

        var owner = NewUser("incident-owner");
        var customer = NewUser("incident-customer");
        var zone = new DeliveryZone($"Incident retry zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Incident retry business {Guid.NewGuid():N}", "SQL test pickup address");
        var order = new Order(customer.Id, business.Id, zone.Id, "SQL test destination", 10m,
            [(Guid.NewGuid(), "Incident retry item", 50m, 1)]);
        var orderId = order.Id;
        var commandId = Guid.NewGuid();
        const string category = "delivery-delay";
        const string description = "Courier reported a delay during offline synchronization.";
        setup.Users.AddRange(owner, customer);
        setup.DeliveryZones.Add(zone);
        setup.Businesses.Add(business);
        setup.Orders.Add(order);
        await setup.SaveChangesAsync();

        try
        {
            var barrier = new TwoPartySaveBarrier();
            async Task<(OrderIncidentView? Result, Exception? Error)> AttemptAsync()
            {
                var attemptOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString)
                    .AddInterceptors(barrier).Options;
                await using var db = new AppDbContext(attemptOptions);
                var service = CreateWorkflowService(db);
                try
                {
                    return (await service.ReportIncidentAsync(orderId, customer.Id, false, commandId, category,
                        description, CancellationToken.None), null);
                }
                catch (Exception exception) { return (null, exception); }
            }

            var attempts = await Task.WhenAll(AttemptAsync(), AttemptAsync());
            Assert.All(attempts, attempt => Assert.Null(attempt.Error));
            var firstResult = Assert.IsType<OrderIncidentView>(attempts[0].Result);
            var secondResult = Assert.IsType<OrderIncidentView>(attempts[1].Result);
            Assert.Equal(firstResult.Id, secondResult.Id);
            Assert.Equal("Open", firstResult.Status);

            setup.ChangeTracker.Clear();
            var persistedOrder = await setup.Orders.Include(candidate => candidate.Incidents)
                .Include(candidate => candidate.StatusHistory).SingleAsync(candidate => candidate.Id == orderId);
            Assert.Single(persistedOrder.Incidents, incident => incident.ReporterUserId == customer.Id && incident.CommandId == commandId);
            Assert.Single(persistedOrder.StatusHistory, change => change.Reason == $"Incident reported: {category}");
        }
        finally
        {
            await CleanupAsync(setup, orderId, business.Id, zone.Id, [owner.Id, customer.Id]);
        }
    }

    private static OrderWorkflowService CreateWorkflowService(AppDbContext db) => new(new OrderWorkflowRepository(db),
        new NullOrderChangePublisher(), new TestCommandSecretProtector(), new NullExternalOfferPublisher());

    private static Order CreateInTransitOrder(ApplicationUser owner, ApplicationUser customer, ApplicationUser reviewer,
        ApplicationUser courier, Business business, DeliveryZone zone)
    {
        var order = new Order(customer.Id, business.Id, zone.Id, "SQL test destination", 10m,
            [(Guid.NewGuid(), "Offline retry item", 50m, 1)]);
        order.SetDeliveryCodeHash(OrderAccessCode.Hash("654321"));
        order.SubmitTransferEvidence("private/sql-test/offline-retry");
        order.ReviewTransfer(reviewer.Id, approved: true);
        order.AcceptByBusiness(owner.Id);
        order.BeginPreparation(owner.Id);
        order.MarkReadyForPickup(owner.Id);
        order.AssignCourier(owner.Id, courier.Id, OrderAccessCode.Hash("123456"), 20m);
        order.MarkPickedUp(courier.Id, OrderAccessCode.Hash("123456"));
        order.BeginDelivery(courier.Id, "call");
        return order;
    }

    private static ApplicationUser NewUser(string suffix)
    {
        var id = Guid.NewGuid();
        var normalized = $"SQLTEST-{suffix}-{id:N}".ToUpperInvariant();
        return new ApplicationUser
        {
            Id = id,
            UserName = normalized,
            NormalizedUserName = normalized,
            Email = $"{normalized.ToLowerInvariant()}@example.test",
            NormalizedEmail = $"{normalized}@EXAMPLE.TEST",
            DisplayName = $"SQL test {suffix}"
        };
    }

    private static async Task CleanupAsync(AppDbContext db, Guid orderId, Guid businessId, Guid zoneId,
        IReadOnlyCollection<Guid> userIds)
    {
        db.ChangeTracker.Clear();
        var order = await db.Orders.SingleOrDefaultAsync(candidate => candidate.Id == orderId);
        if (order is not null) db.Orders.Remove(order);
        await db.SaveChangesAsync();
        var business = await db.Businesses.SingleOrDefaultAsync(candidate => candidate.Id == businessId);
        if (business is not null) db.Businesses.Remove(business);
        var zone = await db.DeliveryZones.SingleOrDefaultAsync(candidate => candidate.Id == zoneId);
        if (zone is not null) db.DeliveryZones.Remove(zone);
        db.Users.RemoveRange(await db.Users.Where(user => userIds.Contains(user.Id)).ToArrayAsync());
        await db.SaveChangesAsync();
    }
}
