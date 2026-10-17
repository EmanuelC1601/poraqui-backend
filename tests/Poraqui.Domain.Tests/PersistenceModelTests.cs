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
                .SingleAsync(x => x.Id == order.Id);

            Assert.Equal(OrderStatus.Confirmed, loaded.Status);
            Assert.Equal(29m, loaded.Total);
            Assert.Equal("Producto de prueba", loaded.Items.Single().ProductName);
            Assert.Equal(reviewer.Id, loaded.TransferReviews.Single().ReviewerUserId);
        }
    }
}
