using Microsoft.Data.SqlClient;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;
using System.Threading;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerExternalOfferConcurrencyTests
{
    [SqlServerIntegrationFact]
    public async Task Concurrent_dispatches_persist_only_one_active_offer_per_courier_and_order()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
        var setupOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var setup = new AppDbContext(setupOptions);
        await setup.Database.MigrateAsync();

        var owner = NewUser("dispatch-owner");
        var customer = NewUser("dispatch-customer");
        var reviewer = NewUser("dispatch-reviewer");
        var couriers = new[] { NewUser("dispatch-courier-a"), NewUser("dispatch-courier-b") };
        var zone = new DeliveryZone($"Dispatch concurrency zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Dispatch concurrency business {Guid.NewGuid():N}", "Test pickup address");
        var order = new Order(customer.Id, business.Id, zone.Id, "Test delivery address", 25m,
            [(Guid.NewGuid(), "Dispatch concurrency item", 100m, 1)]);
        order.SubmitTransferEvidence($"private/sql-test/{Guid.NewGuid():N}");
        order.ReviewTransfer(reviewer.Id, approved: true);
        order.AcceptByBusiness(owner.Id);
        order.BeginPreparation(owner.Id);
        order.MarkReadyForPickup(owner.Id);
        var role = await setup.Roles.SingleOrDefaultAsync(candidate => candidate.Name == ApplicationRoles.ExternalCourier);
        var createdRole = role is null;
        role ??= new IdentityRole<Guid>(ApplicationRoles.ExternalCourier);
        if (createdRole) setup.Roles.Add(role);
        setup.Users.AddRange([owner, customer, reviewer, .. couriers]);
        setup.UserRoles.AddRange(couriers.Select(courier => new IdentityUserRole<Guid> { UserId = courier.Id, RoleId = role.Id }));
        setup.CourierAvailabilities.AddRange(couriers.Select(courier =>
        {
            var availability = new CourierAvailability(courier.Id);
            availability.SetAvailable(true, zone.Id);
            return availability;
        }));
        setup.DeliveryZones.Add(zone);
        setup.Businesses.Add(business);
        setup.Orders.Add(order);
        await setup.SaveChangesAsync();

        var saveBarrier = new TwoPartySaveBarrier();
        try
        {
            async Task<(ExternalOfferDispatchResult? Result, Exception? Error)> DispatchAsync()
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(connectionString)
                    .AddInterceptors(saveBarrier)
                    .Options;
                await using var db = new AppDbContext(options);
                var service = new ExternalCourierService(new ExternalCourierRepository(db), new NullOrderChangePublisher(),
                    new NullExternalOfferPublisher(), new TestExternalOfferSecretProtector());
                try
                {
                    return (await service.CreateOffersAsync(order.Id, owner.Id, isAdmin: false, 35m,
                        "Concurrent dispatch integration test", TimeSpan.FromMinutes(10), CancellationToken.None), null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            var attempts = await Task.WhenAll(DispatchAsync(), DispatchAsync());
            Assert.Single(attempts, attempt => attempt.Result is not null);
            Assert.Single(attempts, attempt => attempt.Error is not null);
            Assert.IsAssignableFrom<DbUpdateException>(attempts.Single(attempt => attempt.Error is not null).Error);

            var activeOffers = await setup.ExternalDeliveryOffers.AsNoTracking()
                .Where(offer => offer.OrderId == order.Id && offer.Status == ExternalDeliveryOfferStatus.Offered)
                .ToArrayAsync();
            Assert.Equal(couriers.Length, activeOffers.Length);
            Assert.Equal(couriers.Length, activeOffers.Select(offer => offer.CourierUserId).Distinct().Count());
        }
        finally
        {
            await CleanupAsync(setup, order.Id, business.Id, zone.Id,
                [owner.Id, customer.Id, reviewer.Id, .. couriers.Select(courier => courier.Id)], removeExternalCourierRole: createdRole);
        }
    }

    [SqlServerIntegrationFact]
    public async Task Concurrent_acceptance_assigns_the_order_to_exactly_one_external_courier()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;

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
                var service = new ExternalCourierService(new ExternalCourierRepository(db), new NullOrderChangePublisher(),
                    new NullExternalOfferPublisher(), new TestExternalOfferSecretProtector());
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
        IReadOnlyCollection<Guid> userIds, bool removeExternalCourierRole = false)
    {
        // The concurrent accept attempts update row-versioned offers and the order.
        // The setup context still tracks the pre-race snapshots, so cleanup must
        // re-read fresh versions before deleting its uniquely identified test data.
        db.ChangeTracker.Clear();
        db.ExternalDeliveryOffers.RemoveRange(await db.ExternalDeliveryOffers.Where(offer => offer.OrderId == orderId).ToArrayAsync());
        var order = await db.Orders.SingleOrDefaultAsync(candidate => candidate.Id == orderId);
        if (order is not null) db.Orders.Remove(order);
        await db.SaveChangesAsync();

        var business = await db.Businesses.SingleOrDefaultAsync(candidate => candidate.Id == businessId);
        if (business is not null) db.Businesses.Remove(business);
        var zone = await db.DeliveryZones.SingleOrDefaultAsync(candidate => candidate.Id == zoneId);
        if (zone is not null) db.DeliveryZones.Remove(zone);
        db.CourierAvailabilities.RemoveRange(await db.CourierAvailabilities.Where(profile => userIds.Contains(profile.CourierUserId)).ToArrayAsync());
        db.UserRoles.RemoveRange(await db.UserRoles.Where(userRole => userIds.Contains(userRole.UserId)).ToArrayAsync());
        db.Users.RemoveRange(await db.Users.Where(user => userIds.Contains(user.Id)).ToArrayAsync());
        if (removeExternalCourierRole)
        {
            var role = await db.Roles.SingleOrDefaultAsync(candidate => candidate.Name == ApplicationRoles.ExternalCourier);
            if (role is not null) db.Roles.Remove(role);
        }
        await db.SaveChangesAsync();
    }
}

public sealed class SqlServerIntegrationFactAttribute : FactAttribute
{
    public const string ConnectionStringEnvironmentVariable = "ConnectionStrings__SqlServerTest";

    public SqlServerIntegrationFactAttribute()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionStringEnvironmentVariable);
        var sharedConnectionString = new ConfigurationBuilder()
            .AddUserSecrets<Program>(optional: true)
            .Build()
            .GetConnectionString("DefaultConnection");
        Skip = GetSkipReason(connectionString, sharedConnectionString);
    }

    internal static string? GetSkipReason(string? connectionString, string? sharedConnectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return $"Set {ConnectionStringEnvironmentVariable} to a private connection string for an isolated SQL Server test database.";
        if (string.IsNullOrWhiteSpace(sharedConnectionString))
            return "SQL Server integration tests require the private DefaultConnection so the test target can be checked against the shared database.";
        try
        {
            var testTarget = new SqlConnectionStringBuilder(connectionString);
            var sharedTarget = new SqlConnectionStringBuilder(sharedConnectionString);
            if (string.Equals(testTarget.InitialCatalog, sharedTarget.InitialCatalog, StringComparison.OrdinalIgnoreCase))
                return "SQL Server integration tests cannot target the configured shared development database.";
            return null;
        }
        catch (ArgumentException)
        {
            return $"{ConnectionStringEnvironmentVariable} is not a valid SQL Server connection string.";
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
