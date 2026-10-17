namespace Poraqui.Domain.Commerce;

public sealed class DeliveryZone
{
    private DeliveryZone() { }

    public DeliveryZone(string name)
    {
        Id = Guid.NewGuid();
        Rename(name);
        IsActive = true;
    }

    public Guid Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("A zone name is required.", nameof(name))
        : name.Trim();

    public void SetActive(bool active) => IsActive = active;
}
