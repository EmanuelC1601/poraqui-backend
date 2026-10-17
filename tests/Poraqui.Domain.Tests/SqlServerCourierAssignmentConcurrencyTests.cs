using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerCourierAssignmentConcurrencyTests
{
    [SqlServerIntegrationFact]
    public async Task Concurrent_internal_assignments_persist_exactly_one_courier()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;

        var setupOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var setup = new AppDbContext(setupOptions);
        await setup.Database.MigrateAsync();

        var owner = NewUser("owner");
        var customer = NewUser("customer");
        var reviewer = NewUser("reviewer");
        var couriers = new[] { NewUser("courier-a"), NewUser("courier-b") };
        var zone = new DeliveryZone($"Internal assignment test zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Internal assignment test business {Guid.NewGuid():N}", "Test pickup address");
        var order = new Order(customer.Id, business.Id, zone.Id, "Test delivery address", 25m,
            [(Guid.NewGuid(), "Concurrency test item", 100m, 1)]);
        order.SetDeliveryCodeHash(OrderAccessCode.Hash("654321"));
        order.SubmitTransferEvidence($"private/sql-test/{Guid.NewGuid():N}");
        order.ReviewTransfer(reviewer.Id, approved: true);
        order.AcceptByBusiness(owner.Id);
        order.BeginPreparation(owner.Id);
        order.MarkReadyForPickup(owner.Id);

        setup.Users.AddRange([owner, customer, reviewer, .. couriers]);
        setup.DeliveryZones.Add(zone);
        setup.Businesses.Add(business);
        setup.Orders.Add(order);

        try
        {
            await setup.SaveChangesAsync();
            var saveBarrier = new TwoPartySaveBarrier();

            async Task<(CourierAssignment? Assignment, Exception? Error)> AttemptAsync(Guid courierId, Guid commandId)
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(connectionString)
                    .AddInterceptors(saveBarrier)
                    .Options;
                await using var db = new AppDbContext(options);
                var service = new OrderWorkflowService(new OrderWorkflowRepository(db), new NullOrderChangePublisher(),
                    new TestCommandSecretProtector());
                try
                {
                    var assignment = await service.AssignCourierAsync(order.Id, owner.Id, courierId, 35m,
                        commandId, CancellationToken.None);
                    return (assignment, null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            var results = await Task.WhenAll(
                AttemptAsync(couriers[0].Id, Guid.NewGuid()),
                AttemptAsync(couriers[1].Id, Guid.NewGuid()));

            Assert.Single(results, result => result.Assignment is not null);
            var rejected = Assert.Single(results, result => result.Error is not null);
            Assert.IsType<CourierAssignmentCommandConflictException>(rejected.Error);
            Assert.Equal(6, results.Single(result => result.Assignment is not null).Assignment!.PickupCode.Length);

            await setup.Entry(order).ReloadAsync();
            var assignmentCommands = await setup.Set<CourierAssignmentCommand>().AsNoTracking()
                .Where(command => command.OrderId == order.Id).ToArrayAsync();
            Assert.Single(assignmentCommands);
            Assert.Contains(couriers, courier => courier.Id == order.CourierUserId);
            Assert.Equal(OrderStatus.AssignedToDelivery, order.Status);
        }
        finally
        {
            await CleanupAsync(setup, order.Id, business.Id, zone.Id,
                [owner.Id, customer.Id, reviewer.Id, .. couriers.Select(courier => courier.Id)]);
        }
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
        db.Set<CourierAssignmentCommand>().RemoveRange(await db.Set<CourierAssignmentCommand>()
            .Where(command => command.OrderId == orderId).ToArrayAsync());
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

internal sealed class TestCommandSecretProtector : IOrderCommandSecretProtector
{
    public string Protect(string secret) => $"test:{secret}";
    public string Unprotect(string protectedSecret) => protectedSecret[5..];
}
