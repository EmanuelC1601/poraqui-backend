namespace Poraqui.Domain.Orders;

public enum ExternalDeliveryOfferStatus { Offered, Accepted, Rejected, Expired, Cancelled }

public sealed class CourierAvailability
{
    private CourierAvailability() { }
    public CourierAvailability(Guid courierUserId) { CourierUserId = courierUserId; IsAvailable = false; UpdatedAtUtc = DateTimeOffset.UtcNow; }
    public Guid CourierUserId { get; private set; }
    public Guid? ZoneId { get; private set; }
    public bool IsAvailable { get; private set; }
    public DateTimeOffset UpdatedAtUtc { get; private set; }
    public void SetAvailable(bool available, Guid? zoneId = null)
    {
        if (available && (zoneId is null || zoneId == Guid.Empty)) throw new ArgumentException("A delivery zone is required while available.", nameof(zoneId));
        IsAvailable = available; ZoneId = available ? zoneId : null; UpdatedAtUtc = DateTimeOffset.UtcNow;
    }
}

public sealed class ExternalDeliveryOffer
{
    private ExternalDeliveryOffer() { }
    public ExternalDeliveryOffer(Guid orderId, Guid courierUserId, Guid zoneId, string pickupAddress,
        string destinationAddress, decimal remuneration, string? conditions, DateTimeOffset expiresAtUtc)
    {
        if (orderId == Guid.Empty || courierUserId == Guid.Empty || zoneId == Guid.Empty) throw new ArgumentException("Order, courier and zone are required.");
        if (string.IsNullOrWhiteSpace(pickupAddress) || string.IsNullOrWhiteSpace(destinationAddress)) throw new ArgumentException("Pickup and destination are required.");
        if (remuneration < 0) throw new ArgumentOutOfRangeException(nameof(remuneration));
        if (expiresAtUtc <= DateTimeOffset.UtcNow) throw new ArgumentOutOfRangeException(nameof(expiresAtUtc));
        Id = Guid.NewGuid(); OrderId = orderId; CourierUserId = courierUserId; ZoneId = zoneId;
        PickupAddress = pickupAddress.Trim(); DestinationAddress = destinationAddress.Trim();
        Remuneration = decimal.Round(remuneration, 2, MidpointRounding.AwayFromZero);
        Conditions = string.IsNullOrWhiteSpace(conditions) ? null : conditions.Trim();
        OfferedAtUtc = DateTimeOffset.UtcNow; ExpiresAtUtc = expiresAtUtc; Status = ExternalDeliveryOfferStatus.Offered;
    }
    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CourierUserId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string PickupAddress { get; private set; } = string.Empty;
    public string DestinationAddress { get; private set; } = string.Empty;
    public decimal Remuneration { get; private set; }
    public string? Conditions { get; private set; }
    public DateTimeOffset OfferedAtUtc { get; private set; }
    public DateTimeOffset ExpiresAtUtc { get; private set; }
    public DateTimeOffset? RespondedAtUtc { get; private set; }
    public ExternalDeliveryOfferStatus Status { get; private set; }
    public long Version { get; private set; } = 1;
    public bool Reject(DateTimeOffset now)
    {
        if (Status == ExternalDeliveryOfferStatus.Rejected) return false;
        EnsureOpen(now); Status = ExternalDeliveryOfferStatus.Rejected; RespondedAtUtc = now; Version++;
        return true;
    }
    public void Accept(DateTimeOffset now)
    {
        EnsureOpen(now); Status = ExternalDeliveryOfferStatus.Accepted; RespondedAtUtc = now; Version++;
    }
    public void Expire(DateTimeOffset now)
    {
        if (Status == ExternalDeliveryOfferStatus.Offered && now >= ExpiresAtUtc) { Status = ExternalDeliveryOfferStatus.Expired; RespondedAtUtc = now; Version++; }
    }
    public void Cancel(DateTimeOffset now)
    {
        if (Status == ExternalDeliveryOfferStatus.Offered) { Status = ExternalDeliveryOfferStatus.Cancelled; RespondedAtUtc = now; Version++; }
    }
    private void EnsureOpen(DateTimeOffset now)
    {
        Expire(now);
        if (Status != ExternalDeliveryOfferStatus.Offered) throw new InvalidOperationException("Offer is no longer available.");
    }
}
