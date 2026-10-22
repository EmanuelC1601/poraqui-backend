using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Identity;
using Poraqui.Application.Orders;
using Poraqui.Application.Security;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Api.Realtime;

[Authorize]
public sealed class OrdersHub(OrderWorkflowService workflow, ExternalCourierService externalCouriers,
    IExternalCourierRepository externalCourierRepository, UserManager<ApplicationUser> users) : Hub
{
    public async Task<OrderRealtimeSnapshot> SubscribeOrder(Guid orderId)
    {
        if (!Guid.TryParse(Context.User?.FindFirstValue(ClaimTypes.NameIdentifier) ??
                           Context.User?.FindFirstValue("sub"), out var actorId))
            throw new HubException("Authenticated user id is unavailable.");

        var hasAdminClaim = Context.User?.IsInRole(ApplicationRoles.PlatformAdmin) == true;
        var isAdmin = hasAdminClaim && await HasCurrentAdminRoleAsync(actorId);
        var subscription = await workflow.GetRealtimeSnapshotAsync(orderId, actorId, isAdmin, Context.ConnectionAborted);
        if (subscription.Audience == "courier" &&
            !await externalCourierRepository.HasDeliveryCourierRoleAsync(actorId, Context.ConnectionAborted))
            throw new HubException("Current courier role is required to subscribe to an order.");
        var group = OrderChangeGroups.For(orderId, subscription.Audience);
        await Groups.AddToGroupAsync(Context.ConnectionId, group, Context.ConnectionAborted);

        try
        {
            // Read after joining so a transition between authorization and group membership is
            // reflected either by this snapshot or by a subsequent group event.
            isAdmin = hasAdminClaim && await HasCurrentAdminRoleAsync(actorId);
            var current = await workflow.GetRealtimeSnapshotAsync(orderId, actorId, isAdmin, Context.ConnectionAborted);
            if (current.Audience == "courier" &&
                !await externalCourierRepository.HasDeliveryCourierRoleAsync(actorId, Context.ConnectionAborted))
                throw new HubException("Current courier role is required to subscribe to an order.");
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
        var principal = Context.User;
        if (principal is null || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var courierId))
            throw new HubException("Authenticated user id is unavailable.");
        if (!await externalCourierRepository.IsExternalCourierAsync(courierId, Context.ConnectionAborted))
            throw new HubException("Only current external couriers can subscribe to delivery offers.");

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
        var principal = Context.User;
        if (principal is null || !Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? principal.FindFirstValue("sub"), out var courierId))
            throw new HubException("Authenticated user id is unavailable.");
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, ExternalOfferChangeGroups.For(courierId), Context.ConnectionAborted);
    }

    private async Task<bool> HasCurrentAdminRoleAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is not null && await users.IsInRoleAsync(user, ApplicationRoles.PlatformAdmin);
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

public sealed class SignalROrderChangePublisher(IHubContext<OrdersHub> hub, ILogger<SignalROrderChangePublisher> logger,
    IExternalCourierRepository couriers)
    : IOrderChangePublisher
{
    public async Task PublishAsync(OrderChangeNotice notice, CancellationToken cancellationToken)
    {
        var payload = new OrderStatusUpdate(notice.OrderId, notice.Status, notice.Version, notice.ChangedAtUtc);
        try
        {
            var groups = new List<string>
            {
                OrderChangeGroups.For(notice.OrderId, "customer"),
                OrderChangeGroups.For(notice.OrderId, "business"),
                OrderChangeGroups.For(notice.OrderId, "admin")
            };
            if (notice.CourierUserId is { } courierId &&
                await couriers.HasDeliveryCourierRoleAsync(courierId, cancellationToken))
                groups.Add(OrderChangeGroups.For(notice.OrderId, "courier"));
            await hub.Clients.Groups(groups).SendAsync("OrderChanged", payload, cancellationToken);
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Could not publish realtime update for order {OrderId} version {Version}; clients can reload the persisted order.",
                notice.OrderId, notice.Version);
        }
    }
}

public sealed record OrderStatusUpdate(Guid OrderId, string Status, long Version, DateTimeOffset ChangedAtUtc);

public sealed class SignalRExternalOfferPublisher(IHubContext<OrdersHub> hub, ILogger<SignalRExternalOfferPublisher> logger,
    IExternalCourierRepository couriers)
    : IExternalOfferPublisher
{
    public async Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken)
    {
        if (!await couriers.IsExternalCourierAsync(courierUserId, cancellationToken)) return;
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
        if (notice.Status != "Cancelled" && !await couriers.IsExternalCourierAsync(courierUserId, cancellationToken)) return;
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
