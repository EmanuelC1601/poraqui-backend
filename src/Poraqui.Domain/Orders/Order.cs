namespace Poraqui.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderLine> _items = [];
    private Order() { }

    public Order(Guid customerUserId, Guid businessId, Guid zoneId, string deliveryAddress, decimal deliveryFee,
        IEnumerable<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity)> items)
    {
        if (customerUserId == Guid.Empty) throw new ArgumentException("A customer is required.", nameof(customerUserId));
        if (businessId == Guid.Empty) throw new ArgumentException("A business is required.", nameof(businessId));
        if (zoneId == Guid.Empty) throw new ArgumentException("A delivery zone is required.", nameof(zoneId));
        if (string.IsNullOrWhiteSpace(deliveryAddress)) throw new ArgumentException("A delivery address is required.", nameof(deliveryAddress));
        if (deliveryFee < 0) throw new ArgumentOutOfRangeException(nameof(deliveryFee));

        var lines = items?.ToArray() ?? throw new ArgumentNullException(nameof(items));
        if (lines.Length == 0) throw new ArgumentException("At least one item is required.", nameof(items));

        Id = Guid.NewGuid();
        OrderNumber = $"PQ-{Guid.NewGuid():N}".ToUpperInvariant();
        CustomerUserId = customerUserId;
        BusinessId = businessId;
        ZoneId = zoneId;
        DeliveryAddress = deliveryAddress.Trim();
        DeliveryFee = decimal.Round(deliveryFee, 2, MidpointRounding.AwayFromZero);
        foreach (var line in lines)
            _items.Add(new OrderLine(line.ProductId, line.ProductName, line.UnitPrice, line.Quantity));
        Total = decimal.Round(_items.Sum(item => item.LineTotal) + DeliveryFee, 2, MidpointRounding.AwayFromZero);
        Status = OrderStatus.PendingPayment;
        CreatedAtUtc = DateTimeOffset.UtcNow;
    }

    public Guid Id { get; private set; }
    public string OrderNumber { get; private set; } = string.Empty;
    public Guid CustomerUserId { get; private set; }
    public Guid BusinessId { get; private set; }
    public Guid ZoneId { get; private set; }
    public string DeliveryAddress { get; private set; } = string.Empty;
    public decimal DeliveryFee { get; private set; }
    public decimal Total { get; private set; }
    public OrderStatus Status { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public IReadOnlyCollection<OrderLine> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TransferReview> TransferReviews => _transferReviews.AsReadOnly();
    private readonly List<TransferReview> _transferReviews = [];

    public void SubmitTransferEvidence(string evidenceReference)
    {
        if (Status != OrderStatus.PendingPayment) throw new InvalidOperationException("Order is not awaiting payment evidence.");
        if (string.IsNullOrWhiteSpace(evidenceReference)) throw new ArgumentException("Evidence reference is required.", nameof(evidenceReference));
        Status = OrderStatus.AwaitingPaymentReview;
        _transferReviews.Add(TransferReview.Submitted(evidenceReference.Trim(), DateTimeOffset.UtcNow));
    }

    public void ReviewTransfer(Guid reviewerUserId, bool approved, string? note = null, DateTimeOffset? reviewedAtUtc = null)
    {
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        if (Status != OrderStatus.AwaitingPaymentReview) throw new InvalidOperationException("Order has no transfer awaiting review.");
        var review = _transferReviews.LastOrDefault(item => item.Decision is null)
            ?? throw new InvalidOperationException("No pending transfer evidence was found.");
        review.RecordDecision(reviewerUserId, approved, note, reviewedAtUtc ?? DateTimeOffset.UtcNow);
        Status = approved ? OrderStatus.Confirmed : OrderStatus.Rejected;
    }

    public void BeginPreparation()
    {
        if (Status != OrderStatus.Confirmed) throw new InvalidOperationException("Only a confirmed order can be prepared.");
        Status = OrderStatus.Preparing;
    }

    public void MarkReadyForPickup()
    {
        if (Status != OrderStatus.Preparing) throw new InvalidOperationException("Only a preparing order can be marked ready.");
        Status = OrderStatus.ReadyForPickup;
    }
}

public sealed class OrderLine
{
    private OrderLine() { }

    internal OrderLine(Guid productId, string productName, decimal unitPrice, int quantity)
    {
        if (productId == Guid.Empty) throw new ArgumentException("A product is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(productName)) throw new ArgumentException("A product name is required.", nameof(productName));
        if (unitPrice < 0) throw new ArgumentOutOfRangeException(nameof(unitPrice));
        if (quantity <= 0) throw new ArgumentOutOfRangeException(nameof(quantity));
        Id = Guid.NewGuid();
        ProductId = productId;
        ProductName = productName.Trim();
        UnitPrice = decimal.Round(unitPrice, 2, MidpointRounding.AwayFromZero);
        Quantity = quantity;
        LineTotal = decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string ProductName { get; private set; } = string.Empty;
    public decimal UnitPrice { get; private set; }
    public int Quantity { get; private set; }
    public decimal LineTotal { get; private set; }
}

public enum TransferDecision { Approved, Rejected }

public sealed class TransferReview
{
    private TransferReview() { }

    internal static TransferReview Submitted(string evidenceReference, DateTimeOffset submittedAtUtc) => new()
    {
        Id = Guid.NewGuid(), EvidenceReference = evidenceReference, SubmittedAtUtc = submittedAtUtc
    };

    public Guid Id { get; private set; }
    public string EvidenceReference { get; private set; } = string.Empty;
    public DateTimeOffset SubmittedAtUtc { get; private set; }
    public Guid? ReviewerUserId { get; private set; }
    public TransferDecision? Decision { get; private set; }
    public string? Note { get; private set; }
    public DateTimeOffset? ReviewedAtUtc { get; private set; }

    internal void RecordDecision(Guid reviewerUserId, bool approved, string? note, DateTimeOffset reviewedAtUtc)
    {
        if (Decision is not null) throw new InvalidOperationException("Transfer evidence has already been reviewed.");
        ReviewerUserId = reviewerUserId;
        Decision = approved ? TransferDecision.Approved : TransferDecision.Rejected;
        Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        ReviewedAtUtc = reviewedAtUtc;
    }
}
