namespace Poraqui.Domain.Orders;

public enum ManualRefundStatus { ReviewRequired, Refunded, NotRequired }

public sealed class ManualRefundCase
{
    private ManualRefundCase() { }

    internal static ManualRefundCase Create(Guid orderId, decimal requestedAmount, Guid requestedByUserId, DateTimeOffset createdAtUtc) => new()
    {
        Id = Guid.NewGuid(),
        OrderId = orderId,
        RequestedAmount = requestedAmount,
        RequestedByUserId = requestedByUserId,
        CreatedAtUtc = createdAtUtc,
        Status = ManualRefundStatus.ReviewRequired
    };

    public Guid Id { get; private set; }
    public Guid OrderId { get; private set; }
    public decimal RequestedAmount { get; private set; }
    public Guid RequestedByUserId { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public ManualRefundStatus Status { get; private set; }
    public decimal? RefundedAmount { get; private set; }
    public string? Reference { get; private set; }
    public string? Note { get; private set; }
    public Guid? ReviewedByUserId { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    internal bool MarkRefunded(Guid reviewerUserId, decimal amount, string reference, string? note,
        DateTimeOffset reviewedAtUtc, decimal orderTotal)
    {
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        if (amount <= 0 || amount > orderTotal) throw new ArgumentOutOfRangeException(nameof(amount));
        var normalizedReference = RequiredText(reference, nameof(reference), 160);
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        if (normalizedNote?.Length > 1000) throw new ArgumentException("Note is too long.", nameof(note));
        if (Status == ManualRefundStatus.Refunded && ReviewedByUserId == reviewerUserId && RefundedAmount == amount &&
            Reference == normalizedReference && Note == normalizedNote) return false;
        if (Status != ManualRefundStatus.ReviewRequired) throw new InvalidOperationException("Refund case has already been reviewed.");
        Status = ManualRefundStatus.Refunded;
        RefundedAmount = amount;
        Reference = normalizedReference;
        Note = normalizedNote;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
        return true;
    }

    internal bool MarkNotRequired(Guid reviewerUserId, string reason, DateTimeOffset reviewedAtUtc)
    {
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        var normalized = RequiredText(reason, nameof(reason), 1000);
        if (Status == ManualRefundStatus.NotRequired && ReviewedByUserId == reviewerUserId && Note == normalized)
            return false;
        if (Status != ManualRefundStatus.ReviewRequired) throw new InvalidOperationException("Refund case has already been reviewed.");
        Status = ManualRefundStatus.NotRequired;
        Note = normalized;
        ReviewedByUserId = reviewerUserId;
        ReviewedAtUtc = reviewedAtUtc;
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
