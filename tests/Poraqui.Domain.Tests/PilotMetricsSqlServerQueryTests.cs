using Microsoft.EntityFrameworkCore;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Domain.Tests;

public sealed class PilotMetricsSqlServerQueryTests
{
    [Fact]
    public void Metrics_queries_translate_with_the_sql_server_provider_without_opening_a_connection()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=localhost;Database=TranslationOnly;Integrated Security=True;TrustServerCertificate=True")
            .Options;
        using var db = new AppDbContext(options);
        var start = DateTimeOffset.UtcNow.AddDays(-7);
        var end = DateTimeOffset.UtcNow;
        var orders = db.Orders.Where(order => order.CreatedAtUtc >= start && order.CreatedAtUtc < end);

        var statusesSql = orders.GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() }).ToQueryString();
        Assert.Contains("CreatedAtUtc", statusesSql, StringComparison.Ordinal);

        var totalsSql = orders.GroupBy(_ => 1).Select(group => new
        {
            OrderCount = group.Count(),
            GrossOrderValue = group.Sum(order => order.Total),
            DeliveredCount = group.Count(order => order.Status == OrderStatus.Delivered),
            CancelledCount = group.Count(order => order.Status == OrderStatus.Cancelled)
        }).ToQueryString();
        Assert.Contains("SUM", totalsSql, StringComparison.OrdinalIgnoreCase);

        var orderIds = orders.Select(order => order.Id);
        var incidentsQuerySql = db.Set<OrderIncident>()
            .Where(incident => incident.Status == OrderIncidentStatus.Open && orderIds.Contains(incident.OrderId))
            .GroupBy(_ => 1).Select(group => group.Count()).ToQueryString();
        Assert.Contains("COUNT", incidentsQuerySql, StringComparison.OrdinalIgnoreCase);

        var refundsSql = db.Set<ManualRefundCase>()
            .Where(refund => refund.Status == ManualRefundStatus.ReviewRequired && orderIds.Contains(refund.OrderId))
            .GroupBy(_ => 1).Select(group => group.Count()).ToQueryString();
        Assert.Contains("COUNT", refundsSql, StringComparison.OrdinalIgnoreCase);

        var deliveriesSql = orders.Where(order => order.Status == OrderStatus.Delivered && order.DeliveredAtUtc != null)
            .Select(order => new { order.CreatedAtUtc, order.DeliveredAtUtc }).ToQueryString();
        Assert.Contains("DeliveredAtUtc", deliveriesSql, StringComparison.Ordinal);
    }
}
