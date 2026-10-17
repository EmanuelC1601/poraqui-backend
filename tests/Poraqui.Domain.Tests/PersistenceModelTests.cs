using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class PersistenceModelTests
{
    [Fact]
    public async Task Commerce_order_and_manual_review_round_trip_in_isolated_sqlite_database()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        await using (var db = new AppDbContext(options))
        {
            await db.Database.EnsureCreatedAsync();
            var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = "owner-test" };
            var customer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "customer-test" };
            var reviewer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "reviewer-test" };
            db.Users.AddRange(owner, customer, reviewer);
            var business = new Business(owner.Id, "Negocio prueba", "Domicilio de prueba");
            var zone = new DeliveryZone("Zona de prueba");
            var product = new CatalogProduct(business.Id, "Producto de prueba", 12.5m);
            var rate = new DeliveryRate(business.Id, zone.Id, 4m);
            var order = new Order(customer.Id, business.Id, zone.Id, "Entrega de prueba", rate.Fee,
                [(product.Id, product.Name, product.Price, 2)]);
            order.SubmitTransferEvidence("fixture-transfer-test-001");
            order.ReviewTransfer(reviewer.Id, approved: true, note: "Prueba aislada");
            db.AddRange(business, zone, product, rate, order);

            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var loaded = await db.Orders
                .Include(x => x.Items)
                .Include(x => x.TransferReviews)
                .Include(x => x.StatusHistory)
                .SingleAsync(x => x.Id == order.Id);

            Assert.Equal(OrderStatus.AwaitingBusinessDecision, loaded.Status);
            Assert.Equal(29m, loaded.Total);
            Assert.Equal("Producto de prueba", loaded.Items.Single().ProductName);
            Assert.Equal(reviewer.Id, loaded.TransferReviews.Single().ReviewerUserId);
            Assert.Equal(new[] { OrderStatus.PendingPayment, OrderStatus.AwaitingPaymentReview, OrderStatus.AwaitingBusinessDecision },
                loaded.StatusHistory.OrderBy(change => change.ChangedAtUtc).Select(change => change.Status).ToArray());
        }
    }

    [Fact]
    public async Task Competing_business_decisions_use_optimistic_concurrency_and_only_one_wins()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = "concurrency-owner" };
        var customer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "concurrency-customer" };
        var reviewer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "concurrency-reviewer" };
        var business = new Business(owner.Id, "Negocio concurrencia", "Centro");
        var zone = new DeliveryZone("Zona concurrencia");
        var product = new CatalogProduct(business.Id, "Producto concurrencia", 10m);
        var order = new Order(customer.Id, business.Id, zone.Id, "Domicilio", 2m,
            [(product.Id, product.Name, product.Price, 1)]);
        order.SubmitTransferEvidence("fixture/concurrency-proof");
        order.ReviewTransfer(reviewer.Id, approved: true);

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.AddRange(owner, customer, reviewer);
            setup.AddRange(business, zone, product, order);
            await setup.SaveChangesAsync();
        }

        await using var left = new AppDbContext(options);
        await using var right = new AppDbContext(options);
        var leftOrder = await left.Orders.Include(item => item.StatusHistory).SingleAsync(item => item.Id == order.Id);
        var rightOrder = await right.Orders.Include(item => item.StatusHistory).SingleAsync(item => item.Id == order.Id);
        leftOrder.AcceptByBusiness(owner.Id);
        rightOrder.RejectByBusiness(owner.Id, "Conflicto de concurrencia");

        await new Poraqui.Infrastructure.Persistence.OrderWorkflowRepository(left).SaveChangesAsync(default);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() =>
            new Poraqui.Infrastructure.Persistence.OrderWorkflowRepository(right).SaveChangesAsync(default));
    }

    [Fact]
    public async Task Expiration_sweep_persists_due_external_offers_without_changing_unexpired_offers()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDbContext>().UseSqlite(connection).Options;
        var owner = new ApplicationUser { Id = Guid.NewGuid(), UserName = "offer-expiry-owner" };
        var customer = new ApplicationUser { Id = Guid.NewGuid(), UserName = "offer-expiry-customer" };
        var courier = new ApplicationUser { Id = Guid.NewGuid(), UserName = "offer-expiry-courier" };
        var zone = new DeliveryZone("Zona oferta expirada");
        var business = new Business(owner.Id, "Negocio expiración", "Origen");
        var order = new Order(customer.Id, business.Id, zone.Id, "Destino", 3m,
            [(Guid.NewGuid(), "Producto", 10m, 1)]);
        var now = DateTimeOffset.UtcNow;
        var due = new ExternalDeliveryOffer(order.Id, courier.Id, zone.Id, "Origen", "Destino", 20m, null,
            now.AddMinutes(5));
        var future = new ExternalDeliveryOffer(order.Id, Guid.NewGuid(), zone.Id, "Origen", "Destino", 20m, null,
            now.AddHours(1));

        await using (var setup = new AppDbContext(options))
        {
            await setup.Database.EnsureCreatedAsync();
            setup.Users.AddRange(owner, customer, courier, new ApplicationUser { Id = future.CourierUserId, UserName = "other-courier" });
            setup.AddRange(zone, business, order, due, future);
            await setup.SaveChangesAsync();
        }

        await using (var db = new AppDbContext(options))
        {
            var repository = new ExternalCourierRepository(db);
            Assert.Equal(1, await repository.ExpireDueOffersAsync(now.AddMinutes(6), default));
        }

        await using var verify = new AppDbContext(options);
        Assert.Equal(ExternalDeliveryOfferStatus.Expired,
            (await verify.ExternalDeliveryOffers.SingleAsync(x => x.Id == due.Id)).Status);
        Assert.Equal(ExternalDeliveryOfferStatus.Offered,
            (await verify.ExternalDeliveryOffers.SingleAsync(x => x.Id == future.Id)).Status);
    }
}
