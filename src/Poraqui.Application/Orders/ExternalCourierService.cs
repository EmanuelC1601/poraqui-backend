using Poraqui.Domain.Orders;
using Poraqui.Application.Commerce;

namespace Poraqui.Application.Orders;

public interface IExternalCourierRepository
{
    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<string?> GetBusinessAddressAsync(Guid businessId, CancellationToken cancellationToken);
    Task<bool> BusinessBelongsToAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> GetAvailableCouriersAsync(Guid zoneId, CancellationToken cancellationToken);
    Task<IReadOnlySet<Guid>> GetCouriersWithActiveOffersAsync(Guid orderId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<CourierAvailability?> GetAvailabilityAsync(Guid courierId, CancellationToken cancellationToken);
    Task<bool> IsActiveZoneAsync(Guid zoneId, CancellationToken cancellationToken);
    Task<bool> IsExternalCourierAsync(Guid courierId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalDeliveryOffer>> GetOffersAsync(Guid courierId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalDeliveryOffer>> GetOffersForOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<ExternalDeliveryOffer?> GetOfferAsync(Guid offerId, CancellationToken cancellationToken);
    Task<bool> HasAcceptedOfferAsync(Guid orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalDeliveryOffer>> ExpireDueOffersAsync(DateTimeOffset now, CancellationToken cancellationToken);
    void AddOffer(ExternalDeliveryOffer offer);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IExternalOfferSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}

public sealed record ExternalOfferView(Guid Id, Guid OrderId, Guid ZoneId, string PickupAddress,
    string DestinationAddress, decimal Remuneration, string? Conditions, DateTimeOffset ExpiresAtUtc, string Status);
public sealed record ExternalOfferAcceptance(Guid OfferId, string PickupCode, decimal Remuneration);
public sealed record ExternalOfferDispatchResult(int AvailableCourierCount, int NewOfferCount);

public sealed class ExternalCourierService(IExternalCourierRepository repository, IOrderChangePublisher publisher,
    IExternalOfferPublisher offerPublisher, IExternalOfferSecretProtector secretProtector)
{
    public async Task SetAvailabilityAsync(Guid courierId, bool available, Guid? zoneId, CancellationToken ct)
    {
        var profile = await repository.GetAvailabilityAsync(courierId, ct);
        if (profile is null) throw new InvalidOperationException("External courier profile is not provisioned.");
        if (available && (zoneId is null || !await repository.IsActiveZoneAsync(zoneId.Value, ct)))
            throw new ArgumentException("An active delivery zone is required while available.", nameof(zoneId));
        profile.SetAvailable(available, zoneId);
        await repository.SaveChangesAsync(ct);
    }

    public async Task<int> ExpireDueOffersAsync(CancellationToken ct)
    {
        var expired = await repository.ExpireDueOffersAsync(DateTimeOffset.UtcNow, ct);
        await PublishChangesAsync(expired, ct);
        return expired.Count;
    }

    public async Task<ExternalOfferDispatchResult> CreateOffersAsync(Guid orderId, Guid businessOwnerId, bool isAdmin, decimal remuneration,
        string? conditions, TimeSpan offerDuration, CancellationToken ct)
    {
        if (remuneration < 0) throw new ArgumentOutOfRangeException(nameof(remuneration));
        if (offerDuration <= TimeSpan.Zero || offerDuration > TimeSpan.FromHours(24)) throw new ArgumentOutOfRangeException(nameof(offerDuration));
        var order = await repository.GetOrderAsync(orderId, ct) ?? throw new KeyNotFoundException("Order was not found.");
        if (!isAdmin && !await repository.BusinessBelongsToAsync(order.BusinessId, businessOwnerId, ct)) throw new OrderResourceForbiddenException();
        if (order.Status != OrderStatus.ReadyForPickup) throw new InvalidOperationException("Order must be ready for pickup before external delivery is offered.");
        if (await repository.HasAcceptedOfferAsync(orderId, ct)) throw new InvalidOperationException("Order already has an accepted external offer.");
        var pickup = await repository.GetBusinessAddressAsync(order.BusinessId, ct) ?? throw new InvalidOperationException("Business pickup address is unavailable.");
        var couriers = await repository.GetAvailableCouriersAsync(order.ZoneId, ct);
        var existing = await repository.GetCouriersWithActiveOffersAsync(order.Id, DateTimeOffset.UtcNow, ct);
        var expiresAt = DateTimeOffset.UtcNow.Add(offerDuration);
        var createdOffers = new List<ExternalDeliveryOffer>();
        foreach (var courierId in couriers)
            if (!existing.Contains(courierId))
            {
                var offer = new ExternalDeliveryOffer(order.Id, courierId, order.ZoneId, pickup, order.DeliveryAddress,
                    remuneration, conditions, expiresAt);
                repository.AddOffer(offer);
                createdOffers.Add(offer);
            }
        await repository.SaveChangesAsync(ct);
        foreach (var offer in createdOffers)
            await offerPublisher.PublishAvailableAsync(offer.CourierUserId,
                new ExternalOfferNotice(offer.Id, offer.OrderId), ct);
        return new ExternalOfferDispatchResult(couriers.Count, couriers.Count(courierId => !existing.Contains(courierId)));
    }

    public async Task<int> CancelOffersAsync(Guid orderId, Guid businessOwnerId, bool isAdmin, CancellationToken ct)
    {
        var order = await repository.GetOrderAsync(orderId, ct) ?? throw new KeyNotFoundException("Order was not found.");
        if (!isAdmin && !await repository.BusinessBelongsToAsync(order.BusinessId, businessOwnerId, ct)) throw new OrderResourceForbiddenException();
        var offers = await repository.GetOffersForOrderAsync(orderId, ct);
        var now = DateTimeOffset.UtcNow;
        foreach (var offer in offers)
        {
            offer.Expire(now);
            if (offer.Status == ExternalDeliveryOfferStatus.Offered) offer.Cancel(now);
        }
        await repository.SaveChangesAsync(ct);
        await PublishChangesAsync(offers, ct);
        return offers.Count(offer => offer.Status == ExternalDeliveryOfferStatus.Cancelled);
    }

    public async Task<IReadOnlyList<ExternalOfferView>> GetOffersAsync(Guid courierId, CancellationToken ct)
    {
        var offers = await repository.GetOffersAsync(courierId, ct);
        var now = DateTimeOffset.UtcNow;
        var due = offers.Where(offer => offer.ExpiresAtUtc <= now).ToArray();
        foreach (var offer in due) offer.Expire(now);
        if (due.Length > 0)
        {
            await repository.SaveChangesAsync(ct);
            await PublishChangesAsync(due, ct);
        }
        return offers.Where(offer => offer.Status == ExternalDeliveryOfferStatus.Offered).Select(ToView).ToArray();
    }

    public async Task RejectAsync(Guid offerId, Guid courierId, CancellationToken ct)
    {
        var offer = await RequiredOffer(offerId, courierId, ct);
        var version = offer.Version;
        try
        {
            if (offer.Reject(DateTimeOffset.UtcNow))
            {
                await repository.SaveChangesAsync(ct);
                await PublishChangesAsync([offer], ct);
            }
        }
        catch (InvalidOperationException)
        {
            if (offer.Status == ExternalDeliveryOfferStatus.Expired && offer.Version != version)
            {
                await repository.SaveChangesAsync(ct);
                await PublishChangesAsync([offer], ct);
            }
            throw;
        }
    }

    public async Task<ExternalOfferAcceptance> AcceptAsync(Guid offerId, Guid courierId, CancellationToken ct)
    {
        var offer = await RequiredOffer(offerId, courierId, ct);
        if (offer.Status == ExternalDeliveryOfferStatus.Accepted)
        {
            var acceptedOrder = await repository.GetOrderAsync(offer.OrderId, ct)
                ?? throw new KeyNotFoundException("Order was not found.");
            if (acceptedOrder.CourierUserId != courierId || string.IsNullOrWhiteSpace(offer.ProtectedPickupCode))
                throw new InvalidOperationException("Accepted offer cannot be replayed by this courier.");
            return new ExternalOfferAcceptance(offer.Id, secretProtector.Unprotect(offer.ProtectedPickupCode), offer.Remuneration);
        }
        if (await repository.HasAcceptedOfferAsync(offer.OrderId, ct)) throw new InvalidOperationException("Order already has an accepted external offer.");
        var order = await repository.GetOrderAsync(offer.OrderId, ct) ?? throw new KeyNotFoundException("Order was not found.");
        var pickupCode = OrderAccessCode.Generate();
        var offerVersion = offer.Version;
        try
        {
            offer.Accept(DateTimeOffset.UtcNow, secretProtector.Protect(pickupCode));
        }
        catch (InvalidOperationException)
        {
            if (offer.Status == ExternalDeliveryOfferStatus.Expired && offer.Version != offerVersion)
            {
                await repository.SaveChangesAsync(ct);
                await PublishChangesAsync([offer], ct);
            }
            throw;
        }
        order.AssignCourier(courierId, courierId, OrderAccessCode.Hash(pickupCode), offer.Remuneration);
        var openOffers = await repository.GetOffersForOrderAsync(offer.OrderId, ct);
        var changedOffers = new List<ExternalDeliveryOffer> { offer };
        var now = DateTimeOffset.UtcNow;
        foreach (var otherOffer in openOffers)
        {
            otherOffer.Expire(now);
            if (otherOffer.Status == ExternalDeliveryOfferStatus.Offered) otherOffer.Cancel(now);
            changedOffers.Add(otherOffer);
        }
        var profile = await repository.GetAvailabilityAsync(courierId, ct);
        profile?.SetAvailable(false);
        await repository.SaveChangesAsync(ct);
        await PublishChangesAsync(changedOffers, ct);
        var change = order.StatusHistory.OrderByDescending(entry => entry.ChangedAtUtc).First();
        await publisher.PublishAsync(new OrderChangeNotice(order.Id, order.BusinessId, order.CustomerUserId,
            order.CourierUserId, order.Status.ToString(), order.Version, change.ChangedAtUtc), ct);
        return new ExternalOfferAcceptance(offer.Id, pickupCode, offer.Remuneration);
    }

    private async Task<ExternalDeliveryOffer> RequiredOffer(Guid offerId, Guid courierId, CancellationToken ct)
    {
        var offer = await repository.GetOfferAsync(offerId, ct);
        if (offer is null) throw new KeyNotFoundException("Offer was not found.");
        if (offer.CourierUserId != courierId) throw new OrderResourceForbiddenException();
        return offer;
    }

    public async Task PublishChangesAsync(IEnumerable<ExternalDeliveryOffer> offers, CancellationToken ct)
    {
        foreach (var offer in offers)
            await offerPublisher.PublishChangedAsync(offer.CourierUserId,
                new ExternalOfferChangedNotice(offer.Id, offer.OrderId, offer.Status.ToString()), ct);
    }

    private static ExternalOfferView ToView(ExternalDeliveryOffer offer) => new(offer.Id, offer.OrderId, offer.ZoneId,
        offer.PickupAddress, offer.DestinationAddress, offer.Remuneration, offer.Conditions, offer.ExpiresAtUtc, offer.Status.ToString());
}
