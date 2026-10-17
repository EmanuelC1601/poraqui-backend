using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Security;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Persistence;

namespace Poraqui.Api.Controllers;

[ApiController]
[Route("api/admin/metrics/pilot")]
[Authorize(Policy = ApplicationPolicies.PlatformAdministration)]
public sealed class PilotMetricsController(AppDbContext db) : ControllerBase
{
    private static readonly TimeSpan MaximumWindow = TimeSpan.FromDays(366);

    [HttpGet]
    public async Task<ActionResult<PilotMetricsResponse>> Get(
        [FromQuery] DateTimeOffset? fromUtc,
        [FromQuery] DateTimeOffset? toUtc,
        CancellationToken cancellationToken)
    {
        var end = toUtc?.ToUniversalTime() ?? DateTimeOffset.UtcNow;
        var start = fromUtc?.ToUniversalTime() ?? end.AddDays(-30);
        if (start >= end || end - start > MaximumWindow)
            return BadRequest(new { error = "The UTC range must be positive and no longer than 366 days." });

        // SQLite does not translate ordering comparisons over DateTimeOffset; the app uses
        // it only in isolated tests. SQL Server executes the bounded aggregate queries below.
        if (db.Database.ProviderName?.Contains("Sqlite", StringComparison.OrdinalIgnoreCase) == true)
        {
            var allOrders = await db.Orders.AsNoTracking().Include(order => order.Incidents)
                .Include(order => order.RefundCase).ToArrayAsync(cancellationToken);
            return Ok(FromOrders(allOrders.Where(order => order.CreatedAtUtc >= start && order.CreatedAtUtc < end), start, end));
        }

        var orders = db.Orders.Where(order => order.CreatedAtUtc >= start && order.CreatedAtUtc < end);
        var statusRows = await orders
            .GroupBy(order => order.Status)
            .Select(group => new { Status = group.Key, Count = group.Count() })
            .ToArrayAsync(cancellationToken);
        var statusCounts = statusRows.Select(item => new PilotOrderStatusCount(item.Status.ToString(), item.Count))
            .OrderBy(item => item.Status).ToArray();

        var totals = await orders
            .GroupBy(_ => 1)
            .Select(group => new
            {
                OrderCount = group.Count(),
                GrossOrderValue = group.Sum(order => order.Total),
                DeliveredCount = group.Count(order => order.Status == OrderStatus.Delivered),
                CancelledCount = group.Count(order => order.Status == OrderStatus.Cancelled)
            })
            .SingleOrDefaultAsync(cancellationToken);
        var orderIds = orders.Select(order => order.Id);
        var openIncidentCount = await db.Set<OrderIncident>()
            .Where(incident => incident.Status == OrderIncidentStatus.Open && orderIds.Contains(incident.OrderId))
            .CountAsync(cancellationToken);
        var refundReviewCount = await db.Set<ManualRefundCase>()
            .Where(refund => refund.Status == ManualRefundStatus.ReviewRequired && orderIds.Contains(refund.OrderId))
            .CountAsync(cancellationToken);

        var deliveredOrders = await orders
            .Where(order => order.Status == OrderStatus.Delivered && order.DeliveredAtUtc != null)
            .Select(order => new { order.CreatedAtUtc, order.DeliveredAtUtc })
            .ToArrayAsync(cancellationToken);
        double? averageMinutes = deliveredOrders.Length == 0 ? null :
            Math.Round(deliveredOrders.Average(order => (order.DeliveredAtUtc!.Value - order.CreatedAtUtc).TotalMinutes), 2);

        return Ok(new PilotMetricsResponse(
            start,
            end,
            totals?.OrderCount ?? 0,
            totals?.GrossOrderValue ?? 0m,
            totals?.DeliveredCount ?? 0,
            totals?.CancelledCount ?? 0,
            averageMinutes,
            openIncidentCount,
            refundReviewCount,
            statusCounts));
    }

    private static PilotMetricsResponse FromOrders(IEnumerable<Order> orders, DateTimeOffset start, DateTimeOffset end)
    {
        var rows = orders.ToArray();
        var delivered = rows.Where(order => order.Status == OrderStatus.Delivered && order.DeliveredAtUtc.HasValue).ToArray();
        return new PilotMetricsResponse(
            start,
            end,
            rows.Length,
            rows.Sum(order => order.Total),
            rows.Count(order => order.Status == OrderStatus.Delivered),
            rows.Count(order => order.Status == OrderStatus.Cancelled),
            delivered.Length == 0 ? null : Math.Round(delivered.Average(order => (order.DeliveredAtUtc!.Value - order.CreatedAtUtc).TotalMinutes), 2),
            rows.Sum(order => order.Incidents.Count(incident => incident.Status == OrderIncidentStatus.Open)),
            rows.Count(order => order.RefundCase?.Status == ManualRefundStatus.ReviewRequired),
            rows.GroupBy(order => order.Status)
                .Select(group => new PilotOrderStatusCount(group.Key.ToString(), group.Count()))
                .OrderBy(item => item.Status).ToArray());
    }
}

public sealed record PilotMetricsResponse(
    DateTimeOffset FromUtc,
    DateTimeOffset ToUtc,
    int OrderCount,
    decimal GrossOrderValue,
    int DeliveredCount,
    int CancelledCount,
    double? AverageMinutesToDelivery,
    int OpenIncidentCount,
    int RefundReviewCount,
    IReadOnlyList<PilotOrderStatusCount> OrdersByStatus);

public sealed record PilotOrderStatusCount(string Status, int Count);
