using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Poraqui.Application.Commerce;
using Poraqui.Application.Orders;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerOrderCreationIdempotencyTests
{
    [SqlServerIntegrationFact]
    public async Task Concurrent_retries_with_the_same_command_create_one_order_and_return_the_same_delivery_code()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
        var setupOptions = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        await using var setup = new AppDbContext(setupOptions);
        await setup.Database.MigrateAsync();

        var owner = NewUser("owner");
        var customer = NewUser("customer");
        var zone = new DeliveryZone($"Order idempotency test zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Order idempotency test business {Guid.NewGuid():N}", "Test pickup address");
        business.SetActive(true);
        business.ConfigureTransferDetails("SQL Test Recipient", "Test Institution", "SQLTEST-ACCOUNT", null);
        var rate = new DeliveryRate(business.Id, zone.Id, 18.50m);
        var product = new CatalogProduct(business.Id, "SQL idempotency item", 42.25m);
        var commandId = Guid.NewGuid();
        var requestItems = new[] { new OrderItemRequest(product.Id, 2) };

        setup.Users.AddRange(owner, customer);
        setup.DeliveryZones.Add(zone);
        setup.Businesses.Add(business);
        setup.DeliveryRates.Add(rate);
        setup.CatalogProducts.Add(product);
        await setup.SaveChangesAsync();

        try
        {
            var saveBarrier = new TwoPartySaveBarrier();
            async Task<(CreatedOrderView? Order, Exception? Error)> AttemptAsync()
            {
                var options = new DbContextOptionsBuilder<AppDbContext>()
                    .UseSqlServer(connectionString)
                    .AddInterceptors(saveBarrier)
                    .Options;
                await using var db = new AppDbContext(options);
                var service = new CatalogOrderService(new CatalogOrderRepository(db), new NullOrderChangePublisher(),
                    new TestCommandSecretProtector());
                try
                {
                    var result = await service.CreateOrderAsync(customer.Id, business.Id, zone.Id, commandId,
                        "Test delivery address", requestItems, CancellationToken.None);
                    return (result, null);
                }
                catch (Exception exception)
                {
                    return (null, exception);
                }
            }

            var attempts = await Task.WhenAll(AttemptAsync(), AttemptAsync());

            Assert.All(attempts, attempt => Assert.Null(attempt.Error));
            var created = Assert.Single(attempts.Select(attempt => attempt.Order).DistinctBy(order => order!.Id));
            Assert.All(attempts, attempt => Assert.Equal(created!.Id, attempt.Order!.Id));
            Assert.All(attempts, attempt => Assert.Equal(created!.CustomerDeliveryCode, attempt.Order!.CustomerDeliveryCode));
            Assert.Equal(84.50m, created!.Subtotal);
            Assert.Equal(18.50m, created.DeliveryFee);
            Assert.Equal(103m, created.Total);

            Assert.Equal(1, await setup.Orders.CountAsync(order => order.CustomerUserId == customer.Id));
            Assert.Equal(1, await setup.Set<OrderCreationCommand>().CountAsync(command =>
                command.CustomerUserId == customer.Id && command.CommandId == commandId));
        }
        finally
        {
            await CleanupAsync(setup, business.Id, zone.Id, product.Id, customer.Id, owner.Id);
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

    private static async Task CleanupAsync(AppDbContext db, Guid businessId, Guid zoneId, Guid productId,
        Guid customerId, Guid ownerId)
    {
        db.ChangeTracker.Clear();
        var commands = await db.Set<OrderCreationCommand>().Where(command => command.CustomerUserId == customerId).ToArrayAsync();
        db.RemoveRange(commands);
        var orders = await db.Orders.Where(order => order.CustomerUserId == customerId).ToArrayAsync();
        db.Orders.RemoveRange(orders);
        await db.SaveChangesAsync();

        var product = await db.CatalogProducts.SingleOrDefaultAsync(item => item.Id == productId);
        if (product is not null) db.CatalogProducts.Remove(product);
        db.DeliveryRates.RemoveRange(await db.DeliveryRates.Where(rate => rate.BusinessId == businessId).ToArrayAsync());
        await db.SaveChangesAsync();

        var business = await db.Businesses.SingleOrDefaultAsync(item => item.Id == businessId);
        if (business is not null) db.Businesses.Remove(business);
        var zone = await db.DeliveryZones.SingleOrDefaultAsync(item => item.Id == zoneId);
        if (zone is not null) db.DeliveryZones.Remove(zone);
        db.Users.RemoveRange(await db.Users.Where(user => user.Id == customerId || user.Id == ownerId).ToArrayAsync());
        await db.SaveChangesAsync();
    }
}
