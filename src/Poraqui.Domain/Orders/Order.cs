namespace Poraqui.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderLine> _items = [];
    private readonly List<OrderStatusChange> _statusHistory = [];
    private Order() { }

    public Order(Guid customerUserId, Guid businessId, Guid zoneId, string deliveryAddress, decimal deliveryFee,
        IEnumerable<(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity)> items,
        OrderTransferDetails? transferDetails = null)
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
        if (transferDetails is not null)
        {
            TransferRecipientName = RequiredText(transferDetails.RecipientName, nameof(transferDetails.RecipientName));
            TransferInstitution = RequiredText(transferDetails.Institution, nameof(transferDetails.Institution));
            TransferAccountReference = RequiredText(transferDetails.AccountReference, nameof(transferDetails.AccountReference));
            TransferInstructions = string.IsNullOrWhiteSpace(transferDetails.Instructions) ? null : transferDetails.Instructions.Trim();
        }
        foreach (var line in lines)
            _items.Add(new OrderLine(line.ProductId, line.ProductName, line.UnitPrice, line.Quantity));
        Total = decimal.Round(_items.Sum(item => item.LineTotal) + DeliveryFee, 2, MidpointRounding.AwayFromZero);
        Status = OrderStatus.PendingPayment;
        Version = 1;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        _statusHistory.Add(new OrderStatusChange(Guid.NewGuid(), customerUserId, Status, CreatedAtUtc, "Order created"));
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
    public long Version { get; private set; }
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? CourierUserId { get; private set; }
    public string? PickupCodeHash { get; private set; }
    public string? DeliveryCodeHash { get; private set; }
    public decimal? CourierPaymentAmount { get; private set; }
    public DateTimeOffset? CourierPaymentReceivedAtUtc { get; private set; }
    public Guid? CourierPaymentReceivedByUserId { get; private set; }
    public string? ContactChannel { get; private set; }
    public DateTimeOffset? DeliveryStartedAtUtc { get; private set; }
    public DateTimeOffset? DeliveredAtUtc { get; private set; }
    public string? TransferRecipientName { get; private set; }
    public string? TransferInstitution { get; private set; }
    public string? TransferAccountReference { get; private set; }
    public string? TransferInstructions { get; private set; }
    public IReadOnlyCollection<OrderLine> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TransferReview> TransferReviews => _transferReviews.AsReadOnly();
    public IReadOnlyCollection<OrderStatusChange> StatusHistory => _statusHistory.AsReadOnly();
    private readonly List<TransferReview> _transferReviews = [];

    public void SubmitTransferEvidence(string evidenceReference, Guid? actorUserId = null)
    {
        if (Status != OrderStatus.PendingPayment) throw new InvalidOperationException("Order is not awaiting payment evidence.");
        if (string.IsNullOrWhiteSpace(evidenceReference)) throw new ArgumentException("Evidence reference is required.", nameof(evidenceReference));
        var now = DateTimeOffset.UtcNow;
        _transferReviews.Add(TransferReview.Submitted(evidenceReference.Trim(), now));
        ChangeStatus(OrderStatus.AwaitingPaymentReview, actorUserId ?? CustomerUserId, "Transfer evidence submitted", now);
    }

    public void ReviewTransfer(Guid reviewerUserId, bool approved, string? note = null, DateTimeOffset? reviewedAtUtc = null)
    {
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        if (Status != OrderStatus.AwaitingPaymentReview) throw new InvalidOperationException("Order has no transfer awaiting review.");
        var review = _transferReviews.LastOrDefault(item => item.Decision is null)
            ?? throw new InvalidOperationException("No pending transfer evidence was found.");
        review.RecordDecision(reviewerUserId, approved, note, reviewedAtUtc ?? DateTimeOffset.UtcNow);
        ChangeStatus(approved ? OrderStatus.AwaitingBusinessDecision : OrderStatus.Rejected,
            reviewerUserId, approved ? "Transfer approved; awaiting business decision" : "Transfer rejected", reviewedAtUtc ?? DateTimeOffset.UtcNow);
    }

    public void AcceptByBusiness(Guid businessUserId)
    {
        EnsureActor(businessUserId);
        EnsureStatus(OrderStatus.AwaitingBusinessDecision);
        ChangeStatus(OrderStatus.Confirmed, businessUserId, "Business accepted order", DateTimeOffset.UtcNow);
    }

    public void RejectByBusiness(Guid businessUserId, string reason)
    {
        EnsureActor(businessUserId);
        EnsureStatus(OrderStatus.AwaitingBusinessDecision);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A rejection reason is required.", nameof(reason));
        ChangeStatus(OrderStatus.Rejected, businessUserId, reason.Trim(), DateTimeOffset.UtcNow);
    }

    public void BeginPreparation(Guid businessUserId)
    {
        EnsureActor(businessUserId);
        EnsureStatus(OrderStatus.Confirmed);
        ChangeStatus(OrderStatus.Preparing, businessUserId, "Preparation started", DateTimeOffset.UtcNow);
    }

    public void MarkReadyForPickup(Guid businessUserId)
    {
        EnsureActor(businessUserId);
        EnsureStatus(OrderStatus.Preparing);
        ChangeStatus(OrderStatus.ReadyForPickup, businessUserId, "Ready for pickup", DateTimeOffset.UtcNow);
    }

    public void AssignCourier(Guid businessUserId, Guid courierUserId, string pickupCodeHash,
        decimal courierPaymentAmount)
    {
        EnsureActor(businessUserId);
        EnsureStatus(OrderStatus.ReadyForPickup);
        if (courierUserId == Guid.Empty) throw new ArgumentException("A courier is required.", nameof(courierUserId));
        if (string.IsNullOrWhiteSpace(pickupCodeHash) || string.IsNullOrWhiteSpace(DeliveryCodeHash))
            throw new ArgumentException("Pickup and delivery codes are required.");
        if (courierPaymentAmount < 0) throw new ArgumentOutOfRangeException(nameof(courierPaymentAmount));
        CourierUserId = courierUserId;
        PickupCodeHash = pickupCodeHash;
        CourierPaymentAmount = decimal.Round(courierPaymentAmount, 2, MidpointRounding.AwayFromZero);
        ChangeStatus(OrderStatus.AssignedToDelivery, businessUserId, "Courier assigned", DateTimeOffset.UtcNow);
    }

    public void SetDeliveryCodeHash(string deliveryCodeHash)
    {
        EnsureStatus(OrderStatus.PendingPayment);
        if (!string.IsNullOrWhiteSpace(DeliveryCodeHash)) throw new InvalidOperationException("Delivery code has already been assigned.");
        if (string.IsNullOrWhiteSpace(deliveryCodeHash)) throw new ArgumentException("Delivery code is required.", nameof(deliveryCodeHash));
        DeliveryCodeHash = deliveryCodeHash;
    }

    public void MarkPickedUp(Guid courierUserId, string submittedCodeHash)
    {
        EnsureCourier(courierUserId);
        EnsureStatus(OrderStatus.AssignedToDelivery);
        EnsureCode(PickupCodeHash, submittedCodeHash, "Pickup code is invalid.");
        var now = DateTimeOffset.UtcNow;
        CourierPaymentReceivedAtUtc = now;
        CourierPaymentReceivedByUserId = courierUserId;
        ChangeStatus(OrderStatus.PickedUp, courierUserId, "Courier confirms business paid at pickup", now);
    }

    public void BeginDelivery(Guid courierUserId, string contactChannel)
    {
        EnsureCourier(courierUserId);
        EnsureStatus(OrderStatus.PickedUp);
        var normalized = contactChannel?.Trim().ToLowerInvariant();
        if (normalized is not ("whatsapp" or "call")) throw new ArgumentException("Contact channel must be whatsapp or call.", nameof(contactChannel));
        ContactChannel = normalized;
        DeliveryStartedAtUtc = DateTimeOffset.UtcNow;
        ChangeStatus(OrderStatus.InTransit, courierUserId, $"Delivery started; contact via {normalized}", DeliveryStartedAtUtc.Value);
    }

    public void MarkDelivered(Guid courierUserId, string submittedCodeHash)
    {
        EnsureCourier(courierUserId);
        EnsureStatus(OrderStatus.InTransit);
        EnsureCode(DeliveryCodeHash, submittedCodeHash, "Delivery code is invalid.");
        DeliveredAtUtc = DateTimeOffset.UtcNow;
        ChangeStatus(OrderStatus.Delivered, courierUserId, "Customer confirmed receipt", DeliveredAtUtc.Value);
    }

    private void EnsureCourier(Guid courierUserId)
    {
        if (courierUserId == Guid.Empty || CourierUserId != courierUserId)
            throw new InvalidOperationException("Only the assigned courier can perform this operation.");
    }

    private void EnsureActor(Guid actorUserId)
    {
        if (actorUserId == Guid.Empty) throw new ArgumentException("An actor is required.", nameof(actorUserId));
    }

    private void EnsureStatus(OrderStatus expected)
    {
        if (Status != expected) throw new InvalidOperationException($"Order must be in {expected} state.");
    }

    private static void EnsureCode(string? storedHash, string? submittedHash, string message)
    {
        if (string.IsNullOrEmpty(storedHash) || string.IsNullOrEmpty(submittedHash) ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                System.Text.Encoding.UTF8.GetBytes(storedHash), System.Text.Encoding.UTF8.GetBytes(submittedHash)))
            throw new InvalidOperationException(message);
    }

    private void ChangeStatus(OrderStatus status, Guid actorUserId, string reason, DateTimeOffset changedAtUtc)
    {
        Status = status;
        Version++;
        _statusHistory.Add(new OrderStatusChange(Guid.NewGuid(), actorUserId, status, changedAtUtc, reason));
    }

    private static string RequiredText(string? value, string parameter) => string.IsNullOrWhiteSpace(value)
        ? throw new ArgumentException("A value is required.", parameter)
        : value.Trim();
}

public sealed record OrderTransferDetails(string RecipientName, string Institution, string AccountReference, string? Instructions);

public sealed class OrderStatusChange(Guid id, Guid actorUserId, OrderStatus status, DateTimeOffset changedAtUtc, string reason)
{
    private OrderStatusChange() : this(Guid.Empty, Guid.Empty, default, default, string.Empty) { }
    public Guid Id { get; private set; } = id;
    public Guid ActorUserId { get; private set; } = actorUserId;
    public OrderStatus Status { get; private set; } = status;
    public DateTimeOffset ChangedAtUtc { get; private set; } = changedAtUtc;
    public string Reason { get; private set; } = reason;
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
