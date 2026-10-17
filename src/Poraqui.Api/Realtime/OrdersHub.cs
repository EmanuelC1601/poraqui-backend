using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;

namespace Poraqui.Api.Realtime;

[Authorize]
public sealed class OrdersHub(OrderWorkflowService workflow, ExternalCourierService externalCouriers) : Hub
{
    public async Task<OrderRealtimeSnapshot> SubscribeOrder(Guid orderId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           Context.User?.FindFirstValue("sub"), out var actorId))
            throw new HubException("Authenticated user id is unavailable.");

        var isAdmin = Context.User?.IsInRole(ApplicationRoles.PlatformAdmin) == true;
        var subscription = await workflow.GetRealtimeSnapshotAsync(orderId, actorId, isAdmin, Context.ConnectionAborted);
        var group = OrderChangeGroups.For(orderId, subscription.Audience);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        try
        {
            // Read after joining so a transition between authorization and group membership is
            // reflected either by this snapshot or by a subsequent group event.
            var current = await workflow.GetRealtimeSnapshotAsync(orderId, actorId, isAdmin, Context.ConnectionAborted);
            if (current.Audience != subscription.Audience)
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
                group = OrderChangeGroups.For(orderId, current.Audience);
                await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
                current = await workflow.GetRealtimeSnapshotAsync(orderId, actorId, isAdmin, Context.ConnectionAborted);
            }
            return current.Snapshot;
        }
        catch
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
            throw;
        }
    }

    public async Task UnsubscribeOrder(Guid orderId)
    {
        foreach (var role in new[] { "customer", "business", "courier", "admin" })
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, OrderChangeGroups.For(orderId, role), Context.ConnectionAborted);
    }

    public async Task<IReadOnlyList<ExternalOfferView>> SubscribeCourierOffers()
    {
        if (Context.User?.IsInRole(ApplicationRoles.ExternalCourier) != true)
            throw new HubException("Only external couriers can subscribe to delivery offers.");
        if (!Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User.FindFirstValue("sub"), out var courierId))
            throw new HubException("Authenticated user id is unavailable.");

        var group = ExternalOfferChangeGroups.For(courierId);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
        try
        {
            return await externalCouriers.GetOffersAsync(courierId, Context.ConnectionAborted);
        }
        catch
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);
            throw;
        }
    }

    public async Task UnsubscribeCourierOffers()
    {
        if (Context.User?.IsInRole(ApplicationRoles.ExternalCourier) != true)
            throw new HubException("Only external couriers can unsubscribe from delivery offers.");
        if (!Guid.TryParse(Context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? Context.User.FindFirstValue("sub"), out var courierId))
            throw new HubException("Authenticated user id is unavailable.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ExternalOfferChangeGroups.For(courierId), Context.ConnectionAborted);
    }
}

public static class OrderChangeGroups
{
    public static string For(Guid orderId, string role) => $"order:{orderId:N}:{role}";
}

public static class ExternalOfferChangeGroups
{
    public static string For(Guid courierUserId) => $"external-offers:{courierUserId:N}";
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

public sealed class SignalRExternalOfferPublisher(IHubContext<OrdersHub> hub, ILogger<SignalRExternalOfferPublisher> logger)
    : IExternalOfferPublisher
{
    public async Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(ExternalOfferChangeGroups.For(courierUserId))
                .SendAsync("ExternalOfferAvailable", notice, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not publish external offer {OfferId} to courier {CourierUserId}; the courier can reload offers.",
                notice.OfferId, courierUserId);
        }
    }

    public async Task PublishChangedAsync(Guid courierUserId, ExternalOfferChangedNotice notice, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Group(ExternalOfferChangeGroups.For(courierUserId))
                .SendAsync("ExternalOfferChanged", notice, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not publish external offer {OfferId} status {Status} to courier {CourierUserId}; the courier can reload offers.",
                notice.OfferId, notice.Status, courierUserId);
        }
    }
}
