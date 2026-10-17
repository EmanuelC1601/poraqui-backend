using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;

namespace Poraqui.Api.Realtime;

[Authorize]
public sealed class OrdersHub(OrderWorkflowService workflow) : Hub
{
    public async Task<OrderRealtimeSnapshot> SubscribeOrder(Guid orderId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           Context.User?.FindFirstValue("sub"), out var actorId))
            throw new HubException("Authenticated user id is unavailable.");

        var isAdmin = Context.User?.IsInRole(ApplicationRoles.PlatformAdmin) == true;
        var subscription = await workflow.GetRealtimeSnapshotAsync(orderId, actorId, isAdmin, Context.ConnectionAborted);
        await Groups.AddToGroupAsync(Context.ConnectionId,
            OrderChangeGroups.For(orderId, subscription.Audience), Context.ConnectionAborted);
        return subscription.Snapshot;
    }

    public async Task UnsubscribeOrder(Guid orderId)
    {
        foreach (var role in new[] { "customer", "business", "courier", "admin" })
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrderChangeGroups.For(orderId, role), Context.ConnectionAborted);
    }
}

public static class OrderChangeGroups
{
    public static string For(Guid orderId, string role) => $"order:{orderId:N}:{role}";
}

public sealed class SignalROrderChangePublisher(IHubContext<OrdersHub> hub, ILogger<SignalROrderChangePublisher> logger)
    : IOrderChangePublisher
{
    public async Task PublishAsync(OrderChangeNotice notice, CancellationToken cancellationToken)
    {
        var payload = new OrderStatusUpdate(notice.OrderId, notice.Status, notice.Version, notice.ChangedAtUtc);
        try
        {
            await hub.Clients.Groups(
                    OrderChangeGroups.For(notice.OrderId, "customer"),
                    OrderChangeGroups.For(notice.OrderId, "business"),
                    OrderChangeGroups.For(notice.OrderId, "courier"),
                    OrderChangeGroups.For(notice.OrderId, "admin"))
                .SendAsync("OrderChanged", payload, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not publish realtime update for order {OrderId} version {Version}; clients can reload the persisted order.",
                notice.OrderId, notice.Version);
        }
    }
}

public sealed record OrderStatusUpdate(Guid OrderId, string Status, long Version, DateTimeOffset ChangedAtUtc);
