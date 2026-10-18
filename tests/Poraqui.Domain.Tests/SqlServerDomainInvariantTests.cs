using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class SqlServerDomainInvariantTests
{
    [SqlServerIntegrationFact]
    public async Task Sql_server_rejects_rows_that_violate_domain_invariants()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlServerIntegrationFactAttribute.ConnectionStringEnvironmentVariable)!;
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlServer(connectionString).Options;
        var owner = NewUser("owner");
        var customer = NewUser("customer");
        var courier = NewUser("courier");
        var reviewer = NewUser("reviewer");
        var zone = new DeliveryZone($"Invariant test zone {Guid.NewGuid():N}");
        var business = new Business(owner.Id, $"Invariant test business {Guid.NewGuid():N}", "Test pickup");
        business.ConfigureOpeningHours("UTC", Enum.GetValues<DayOfWeek>().Select(day =>
            (day, IsClosed: false, OpensAt: (TimeOnly?)new TimeOnly(9, 0), ClosesAt: (TimeOnly?)new TimeOnly(17, 0))));
        var product = new CatalogProduct(business.Id, "Invariant test product", 100m);
        var optionGroup = product.AddOptionGroup("Choices", 0, 1, [("Option", 0m)]);
        var option = optionGroup.Options.Single();
        var rate = new DeliveryRate(business.Id, zone.Id, 25m);
        var order = new Order(customer.Id, business.Id, zone.Id, "Test destination", rate.Fee,
            [(product.Id, product.Name, product.Price, 1)]);
        order.SubmitTransferEvidence($"private/sql-test/{Guid.NewGuid():N}");
        order.ReviewTransfer(reviewer.Id, approved: true);
        order.CancelByBusiness(owner.Id, "Invariant test cancellation");
        var offer = new ExternalDeliveryOffer(order.Id, courier.Id, zone.Id,
            "Test pickup", "Test destination", 30m, null, DateTimeOffset.UtcNow.AddMinutes(10));
        var availability = new CourierAvailability(courier.Id);

        try
        {
            await using (var setup = new AppDbContext(options))
            {
                await setup.Database.MigrateAsync();
                setup.Users.AddRange(owner, customer, courier, reviewer);
                setup.AddRange(zone, business, product, rate, order, offer, availability);
                await setup.SaveChangesAsync();
            }

            await using var connection = new SqlConnection(connectionString);
            await connection.OpenAsync();

            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.CatalogProducts SET Price = -1 WHERE Id = @id", product.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.ProductOptions SET PriceDelta = -1 WHERE Id = @id", option.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.ProductOptionGroups SET MaxSelections = 0 WHERE Id = @id", optionGroup.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.DeliveryRates SET Fee = -1 WHERE Id = @id", rate.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.Orders SET Total = -1 WHERE Id = @id", order.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.OrderLines SET Quantity = 0 WHERE OrderId = @id", order.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.ExternalDeliveryOffers SET Remuneration = -1 WHERE Id = @id", offer.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.CourierAvailabilities SET IsAvailable = 1 WHERE CourierUserId = @id", courier.Id);
            await AssertConstraintViolationAsync(connection,
                "UPDATE dbo.ManualRefundCases SET RefundedAmount = RequestedAmount + 1 WHERE OrderId = @id", order.Id);
            await AssertOpeningHoursConstraintViolationAsync(connection, business.Id);
        }
        finally
        {
            await CleanupAsync(options, order.Id, business.Id, zone.Id, offer.Id, courier.Id,
                [owner.Id, customer.Id, courier.Id, reviewer.Id]);
        }
    }

    private static async Task AssertConstraintViolationAsync(SqlConnection connection, string sql, Guid id)
    {
        await using var command = new SqlCommand(sql, connection);
        command.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.UniqueIdentifier) { Value = id });
        var exception = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(547, exception.Number);
    }

    private static async Task AssertOpeningHoursConstraintViolationAsync(SqlConnection connection, Guid businessId)
    {
        await using var command = new SqlCommand(
            "UPDATE dbo.BusinessOpeningHours SET OpensAt = '17:00', ClosesAt = '09:00' WHERE BusinessId = @id AND Day = 0", connection);
        command.Parameters.Add(new SqlParameter("@id", System.Data.SqlDbType.UniqueIdentifier) { Value = businessId });
        var exception = await Assert.ThrowsAsync<SqlException>(() => command.ExecuteNonQueryAsync());
        Assert.Equal(547, exception.Number);
    }

    private static async Task CleanupAsync(DbContextOptions<AppDbContext> options, Guid orderId, Guid businessId,
        Guid zoneId, Guid offerId, Guid courierId, IReadOnlyCollection<Guid> userIds)
    {
        await using var db = new AppDbContext(options);
        await db.ExternalDeliveryOffers.Where(item => item.Id == offerId).ExecuteDeleteAsync();
        await db.CourierAvailabilities.Where(item => item.CourierUserId == courierId).ExecuteDeleteAsync();
        await db.Orders.Where(item => item.Id == orderId).ExecuteDeleteAsync();
        await db.CatalogProducts.Where(item => item.BusinessId == businessId).ExecuteDeleteAsync();
        await db.DeliveryRates.Where(item => item.BusinessId == businessId).ExecuteDeleteAsync();
        await db.Businesses.Where(item => item.Id == businessId).ExecuteDeleteAsync();
        await db.DeliveryZones.Where(item => item.Id == zoneId).ExecuteDeleteAsync();
        await db.Users.Where(item => userIds.Contains(item.Id)).ExecuteDeleteAsync();
    }

    private static ApplicationUser NewUser(string suffix)
    {
        var id = Guid.NewGuid();
        var normalized = $"SQLTEST-INVARIANT-{suffix}-{id:N}".ToUpperInvariant();
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
}
