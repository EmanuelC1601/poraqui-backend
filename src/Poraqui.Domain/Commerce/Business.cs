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
    public string? TransferRecipientName { get; private set; }
    public string? TransferInstitution { get; private set; }
    public string? TransferAccountReference { get; private set; }
    public string? TransferInstructions { get; private set; }

    public bool HasTransferDetails => !string.IsNullOrWhiteSpace(TransferRecipientName) &&
        !string.IsNullOrWhiteSpace(TransferInstitution) && !string.IsNullOrWhiteSpace(TransferAccountReference);

    public void Rename(string name) => Name = RequiredText(name, nameof(name));
    public void ChangeAddress(string address) => Address = RequiredText(address, nameof(address));
    public void SetActive(bool active) => IsActive = active;

    public void ConfigureTransferDetails(string recipientName, string institution, string accountReference, string? instructions)
    {
        var normalizedRecipient = RequiredText(recipientName, nameof(recipientName));
        var normalizedInstitution = RequiredText(institution, nameof(institution));
        var normalizedAccount = RequiredText(accountReference, nameof(accountReference));
        var normalizedInstructions = string.IsNullOrWhiteSpace(instructions) ? null : instructions.Trim();
        if (normalizedRecipient.Length > 160 || normalizedInstitution.Length > 120 || normalizedAccount.Length > 120 ||
            (normalizedInstructions?.Length ?? 0) > 500)
            throw new ArgumentException("Transfer details exceed the allowed field lengths.");
        TransferRecipientName = normalizedRecipient;
        TransferInstitution = normalizedInstitution;
        TransferAccountReference = normalizedAccount;
        TransferInstructions = normalizedInstructions;
    }

    private static string RequiredText(string value, string parameter)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized)
            ? throw new ArgumentException("A value is required.", parameter)
            : normalized;
    }
}
