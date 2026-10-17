namespace Poraqui.Domain.Commerce;

public sealed class DeliveryRate
{
    private DeliveryRate() { }

    public DeliveryRate(Guid businessId, Guid zoneId, decimal fee)
    {
        if (businessId == Guid.Empty) throw new ArgumentException("A business is required.", nameof(businessId));
        if (zoneId == Guid.Empty) throw new ArgumentException("A zone is required.", nameof(zoneId));
        if (fee < 0) throw new ArgumentOutOfRangeException(nameof(fee), "Delivery fee cannot be negative.");
        Id = Guid.NewGuid();
        BusinessId = businessId;
        ZoneId = zoneId;
        Fee = decimal.Round(fee, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid ZoneId { get; private set; }
    public decimal Fee { get; private set; }
    public bool IsActive { get; private set; } = true;

    public void ChangeFee(decimal fee) => Fee = fee < 0
        ? throw new ArgumentOutOfRangeException(nameof(fee), "Delivery fee cannot be negative.")
        : decimal.Round(fee, 2, MidpointRounding.AwayFromZero);

    public void SetActive(bool active) => IsActive = active;
}
