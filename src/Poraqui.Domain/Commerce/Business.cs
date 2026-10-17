namespace Poraqui.Domain.Commerce;

public sealed class Business
{
    private readonly List<BusinessOpeningHours> _openingHours = [];
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
    public string TimeZoneId { get; private set; } = "UTC";
    public bool HasConfiguredOpeningHours { get; private set; }
    public IReadOnlyCollection<BusinessOpeningHours> OpeningHours => _openingHours.AsReadOnly();

    public bool HasTransferDetails => !string.IsNullOrWhiteSpace(TransferRecipientName) &&
        !string.IsNullOrWhiteSpace(TransferInstitution) && !string.IsNullOrWhiteSpace(TransferAccountReference);

    public void Rename(string name) => Name = RequiredText(name, nameof(name));
    public void ChangeAddress(string address) => Address = RequiredText(address, nameof(address));
    public void SetActive(bool active) => IsActive = active;

    public void ConfigureOpeningHours(string timeZoneId,
        IEnumerable<(DayOfWeek Day, bool IsClosed, TimeOnly? OpensAt, TimeOnly? ClosesAt)> schedule)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId)) throw new ArgumentException("A time zone is required.", nameof(timeZoneId));
        var normalizedTimeZone = timeZoneId.Trim();
        if (normalizedTimeZone.Length > 100) throw new ArgumentException("The time zone identifier is too long.", nameof(timeZoneId));
        try { _ = TimeZoneInfo.FindSystemTimeZoneById(normalizedTimeZone); }
        catch (TimeZoneNotFoundException exception) { throw new ArgumentException("The time zone is not supported by this server.", nameof(timeZoneId), exception); }
        catch (InvalidTimeZoneException exception) { throw new ArgumentException("The time zone is invalid.", nameof(timeZoneId), exception); }

        var entries = schedule?.ToArray() ?? throw new ArgumentNullException(nameof(schedule));
        if (entries.Length != 7 || entries.Select(entry => entry.Day).Distinct().Count() != 7 ||
            entries.Any(entry => !Enum.IsDefined(entry.Day)))
            throw new ArgumentException("Opening hours must contain exactly one entry for each day of the week.", nameof(schedule));
        foreach (var entry in entries)
        {
            if (entry.IsClosed)
            {
                if (entry.OpensAt is not null || entry.ClosesAt is not null)
                    throw new ArgumentException("Closed days cannot include opening or closing times.", nameof(schedule));
            }
            else if (entry.OpensAt is null || entry.ClosesAt is null || entry.OpensAt >= entry.ClosesAt)
                throw new ArgumentException("Open days require a closing time later than their opening time.", nameof(schedule));
        }

        _openingHours.Clear();
        _openingHours.AddRange(entries.Select(entry => new BusinessOpeningHours(Id, entry.Day,
            entry.IsClosed, entry.OpensAt, entry.ClosesAt)));
        TimeZoneId = normalizedTimeZone;
        HasConfiguredOpeningHours = true;
    }

    public bool IsAcceptingOrdersAt(DateTimeOffset utcNow)
    {
        if (!HasConfiguredOpeningHours) return true;
        var local = TimeZoneInfo.ConvertTime(utcNow, TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId));
        var today = _openingHours.SingleOrDefault(entry => entry.Day == local.DayOfWeek);
        if (today is null || today.IsClosed || today.OpensAt is null || today.ClosesAt is null) return false;
        var localTime = TimeOnly.FromDateTime(local.DateTime);
        return localTime >= today.OpensAt && localTime < today.ClosesAt;
    }

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

public sealed class BusinessOpeningHours
{
    private BusinessOpeningHours() { }

    internal BusinessOpeningHours(Guid businessId, DayOfWeek day, bool isClosed, TimeOnly? opensAt, TimeOnly? closesAt)
    {
        BusinessId = businessId;
        Day = day;
        IsClosed = isClosed;
        OpensAt = opensAt;
        ClosesAt = closesAt;
    }

    public Guid BusinessId { get; private set; }
    public DayOfWeek Day { get; private set; }
    public bool IsClosed { get; private set; }
    public TimeOnly? OpensAt { get; private set; }
    public TimeOnly? ClosesAt { get; private set; }
}
