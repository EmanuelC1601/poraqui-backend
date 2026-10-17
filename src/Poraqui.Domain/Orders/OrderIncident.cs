namespace Poraqui.Domain.Orders;

public enum OrderIncidentStatus { Open, Resolved }

public sealed class OrderIncident
{
    private OrderIncident() { }

    internal OrderIncident(Guid orderId, Guid reporterUserId, string category, string description, DateTimeOffset reportedAtUtc,
        Guid? commandId = null)
    {
        if (orderId == Guid.Empty) throw new ArgumentException("An order is required.", nameof(orderId));
        if (reporterUserId == Guid.Empty) throw new ArgumentException("A reporter is required.", nameof(reporterUserId));
        Category = RequiredText(category, nameof(category), 80);
        Description = RequiredText(description, nameof(description), 2000);
        Id = Guid.NewGuid();
        OrderId = orderId;
        ReporterUserId = reporterUserId;
        CommandId = commandId;
        ReportedAtUtc = reportedAtUtc;
        Status = OrderIncidentStatus.Open;
    }

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid ReporterUserId { get; private set; }
    public Guid? CommandId { get; private set; }
    public string Category { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public DateTimeOffset ReportedAtUtc { get; private set; }
    public OrderIncidentStatus Status { get; private set; }
    public Guid? ResolverUserId { get; private set; }
    public string? Resolution { get; private set; }
    public DateTimeOffset? ResolvedAtUtc { get; private set; }

    internal bool Resolve(Guid resolverUserId, string resolution, DateTimeOffset resolvedAtUtc)
    {
        if (resolverUserId == Guid.Empty) throw new ArgumentException("A resolver is required.", nameof(resolverUserId));
        var normalized = RequiredText(resolution, nameof(resolution), 2000);
        if (Status == OrderIncidentStatus.Resolved && ResolverUserId == resolverUserId && Resolution == normalized)
            return false;
        if (Status != OrderIncidentStatus.Open) throw new InvalidOperationException("Incident has already been resolved.");
        ResolverUserId = resolverUserId;
        Resolution = normalized;
        ResolvedAtUtc = resolvedAtUtc;
        Status = OrderIncidentStatus.Resolved;
        return true;
    }

    private static string RequiredText(string value, string parameter, int maximumLength)
    {
        var normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized) || normalized.Length > maximumLength)
            throw new ArgumentException($"Value must contain between 1 and {maximumLength} characters.", parameter);
        return normalized;
    }
}
