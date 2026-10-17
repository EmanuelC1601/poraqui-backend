using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;
using System.Threading;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerExternalOfferConcurrencyTests
{
    [SqlServerIntegrationFact]
    public async Task Concurrent_acceptance_assigns_the_order_to_exactly_one_external_courier()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
        var connection = new SqlConnectionStringBuilder(connectionString);
        Assert.Equal(SqlServerIntegrationFactAttribute.RequiredDatabaseName, connection.InitialCatalog,
            ignoreCase: true);

        var setupOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var setup = new AppDbContext(setupOptions);
        await setup.Database.MigrateAsync();

        var owner = NewUser("owner");
        var customer = NewUser("customer");
        var reviewer = NewUser("reviewer");
        var couriers = new[] { NewUser("courier-a"), NewUser("courier-b") };
        var zone = new DeliveryZone($"Concurrency test zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Concurrency test business {Guid.NewGuid():N}", "Test pickup address");
        var order = new Order(customer.Id, business.Id, zone.Id, "Test delivery address", 25m,
            [(Guid.NewGuid(), "Concurrency test item", 100m, 1)]);
        order.SetDeliveryCodeHash(OrderAccessCode.Hash("654321"));
        order.SubmitTransferEvidence($"private/sql-test-{Guid.NewGuid():N}");
        order.ReviewTransfer(reviewer.Id, approved: true);
        order.AcceptByBusiness(owner.Id);
        order.BeginPreparation(owner.Id);
        order.MarkReadyForPickup(owner.Id);
        var offers = couriers.Select(courier => new ExternalDeliveryOffer(order.Id, courier.Id, zone.Id,
            business.Address, order.DeliveryAddress, 35m, "SQL concurrency integration test", DateTimeOffset.UtcNow.AddMinutes(10))).ToArray();

        setup.Users.AddRange([owner, customer, reviewer, .. couriers]);
        setup.DeliveryZones.Add(zone);
        setup.Businesses.Add(business);
        setup.Orders.Add(order);
        setup.ExternalDeliveryOffers.AddRange(offers);
        await setup.SaveChangesAsync();

        var saveBarrier = new TwoPartySaveBarrier();
        try
        {
            async Task<(Guid? OfferId, Exception? Error)> AttemptAsync(Guid offerId, Guid courierId)
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(connectionString)
                    .AddInterceptors(saveBarrier)
                    .Options;
                await using var db = new AppDbContext(options);
                var service = new ExternalCourierService(new ExternalCourierRepository(db), new NullOrderChangePublisher());
                try
                {
                    var accepted = await service.AcceptAsync(offerId, courierId, CancellationToken.None);
                    return (accepted.OfferId, null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            var attempts = await Task.WhenAll(
                AttemptAsync(offers[0].Id, couriers[0].Id),
                AttemptAsync(offers[1].Id, couriers[1].Id));

            Assert.Single(attempts, attempt => attempt.OfferId is not null);
            Assert.Single(attempts, attempt => attempt.Error is not null);
            Assert.All(attempts.Where(attempt => attempt.Error is not null), attempt =>
                Assert.True(attempt.Error is DbUpdateConcurrencyException or DbUpdateException,
                    $"Unexpected concurrent acceptance failure: {attempt.Error.GetType().Name}"));

            await setup.Entry(order).ReloadAsync();
            var acceptedOffers = await setup.ExternalDeliveryOffers.AsNoTracking()
                .Where(offer => offer.OrderId == order.Id && offer.Status == ExternalDeliveryOfferStatus.Accepted)
                .ToArrayAsync();
            Assert.Single(acceptedOffers);
            Assert.Equal(acceptedOffers[0].CourierUserId, order.CourierUserId);
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
        db.ExternalDeliveryOffers.RemoveRange(await db.ExternalDeliveryOffers.Where(offer => offer.OrderId == orderId).ToArrayAsync());
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

public sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__SqlServerTest";
    public const string RequiredDatabaseName = "PoraquiIntegrationTests";

    public SqlServerIntegrationFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            Skip = $"Set {ConnectionStringEnvironmentVariable} to a private connection string for an isolated {RequiredDatabaseName} database.";
            return;
        }

        try
        {
            var builder = new SqlConnectionStringBuilder(connectionString);
            if (!string.Equals(builder.InitialCatalog, RequiredDatabaseName, StringComparison.OrdinalIgnoreCase))
                Skip = $"SQL Server integration tests only run against the isolated {RequiredDatabaseName} database.";
        }
        catch (ArgumentException)
        {
            Skip = $"{ConnectionStringEnvironmentVariable} is not a valid SQL Server connection string.";
        }
    }
}

internal sealed class TwoPartySaveBarrier : SaveChangesInterceptor
{
    private readonly TaskCompletionSource _bothSaving = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _arrivals;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Interlocked.Increment(ref _arrivals) == 2) _bothSaving.TrySetResult();
        await _bothSaving.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        return result;
    }
}
