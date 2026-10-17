namespace Poraqui.Domain.Commerce;

public sealed class Business
{
    private Business() { }

    public Business(Guid ownerUserId, string name, string address)
    {
        if (ownerUserId == Guid.Empty) throw new ArgumentException("A business owner is required.", nameof(ownerUserId));
        Id = Guid.NewGuid();
        OwnerUserId = ownerUserId;
        Rename(name);
        ChangeAddress(address);
    }

    public Guid Id { get; private set; }
    public Guid OwnerUserId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Address { get; private set; } = string.Empty;
    public bool IsActive { get; private set; }

    public void Rename(string name) => Name = RequiredText(name, nameof(name));
    public void ChangeAddress(string address) => Address = RequiredText(address, nameof(address));
    public void SetActive(bool active) => IsActive = active;

    private static string RequiredText(string value, string parameter)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("A value is required.", parameter)
            : normalized;
    }
}
