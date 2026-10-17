namespace Poraqui.Domain.Orders;

public sealed class Order
{
    private readonly List<OrderLine> _items = [];
    private readonly List<OrderStatusChange> _statusHistory = [];
    private readonly List<OrderIncident> _incidents = [];
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
    public DateTimeOffset? CancelledAtUtc { get; private set; }
    public Guid? CancelledByUserId { get; private set; }
    public string? CancellationReason { get; private set; }
    public ManualRefundCase? RefundCase { get; private set; }
    public string? TransferRecipientName { get; private set; }
    public string? TransferInstitution { get; private set; }
    public string? TransferAccountReference { get; private set; }
    public string? TransferInstructions { get; private set; }
    public IReadOnlyCollection<OrderLine> Items => _items.AsReadOnly();
    public IReadOnlyCollection<TransferReview> TransferReviews => _transferReviews.AsReadOnly();
    public IReadOnlyCollection<OrderStatusChange> StatusHistory => _statusHistory.AsReadOnly();
    public IReadOnlyCollection<OrderIncident> Incidents => _incidents.AsReadOnly();
    private readonly List<TransferReview> _transferReviews = [];

    public void SubmitTransferEvidence(string evidenceReference, Guid? actorUserId = null)
    {
        if (Status != OrderStatus.PendingPayment) throw new InvalidOperationException("Order is not awaiting payment evidence.");
        if (string.IsNullOrWhiteSpace(evidenceReference)) throw new ArgumentException("Evidence reference is required.", nameof(evidenceReference));
        var now = DateTimeOffset.UtcNow;
        _transferReviews.Add(TransferReview.Submitted(evidenceReference.Trim(), now));
        ChangeStatus(OrderStatus.AwaitingPaymentReview, actorUserId ?? CustomerUserId, "Transfer evidence submitted", now);
    }

    public bool ReviewTransfer(Guid reviewerUserId, bool approved, string? note = null, DateTimeOffset? reviewedAtUtc = null)
    {
        if (reviewerUserId == Guid.Empty) throw new ArgumentException("A reviewer is required.", nameof(reviewerUserId));
        var normalizedNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        var priorReview = _transferReviews.LastOrDefault();
        if (priorReview?.Decision == (approved ? TransferDecision.Approved : TransferDecision.Rejected) &&
            priorReview.ReviewerUserId == reviewerUserId && priorReview.Note == normalizedNote) return false;
        if (Status != OrderStatus.AwaitingPaymentReview) throw new InvalidOperationException("Order has no transfer awaiting review.");
        var review = _transferReviews.LastOrDefault(item => item.Decision is null)
            ?? throw new InvalidOperationException("No pending transfer evidence was found.");
        review.RecordDecision(reviewerUserId, approved, note, reviewedAtUtc ?? DateTimeOffset.UtcNow);
        ChangeStatus(approved ? OrderStatus.AwaitingBusinessDecision : OrderStatus.Rejected,
            reviewerUserId, approved ? "Transfer approved; awaiting business decision" : "Transfer rejected", reviewedAtUtc ?? DateTimeOffset.UtcNow);
        return true;
    }

    public bool AcceptByBusiness(Guid businessUserId)
    {
        EnsureActor(businessUserId);
        if (HasRecordedChange(OrderStatus.Confirmed, businessUserId, "Business accepted order")) return false;
        EnsureStatus(OrderStatus.AwaitingBusinessDecision);
        ChangeStatus(OrderStatus.Confirmed, businessUserId, "Business accepted order", DateTimeOffset.UtcNow);
        return true;
    }

    public bool RejectByBusiness(Guid businessUserId, string reason)
    {
        EnsureActor(businessUserId);
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("A rejection reason is required.", nameof(reason));
        var normalizedReason = reason.Trim();
        if (HasRecordedChange(OrderStatus.Rejected, businessUserId, normalizedReason)) return false;
        EnsureStatus(OrderStatus.AwaitingBusinessDecision);
        ChangeStatus(OrderStatus.Rejected, businessUserId, normalizedReason, DateTimeOffset.UtcNow);
        EnsureRefundFollowUp(businessUserId);
        return true;
    }

    public bool CancelByCustomer(Guid customerUserId, string reason)
    {
        if (customerUserId != CustomerUserId) throw new InvalidOperationException("Only the customer can cancel this order.");
        return Cancel(customerUserId, reason, [OrderStatus.PendingPayment]);
    }

    public bool CancelByBusiness(Guid businessUserId, string reason)
    {
        EnsureActor(businessUserId);
        return Cancel(businessUserId, reason,
            [OrderStatus.PendingPayment, OrderStatus.AwaitingPaymentReview, OrderStatus.AwaitingBusinessDecision,
                OrderStatus.Confirmed, OrderStatus.Preparing, OrderStatus.ReadyForPickup]);
    }

    public OrderIncident AddIncident(Guid reporterUserId, string category, string description, Guid? commandId = null)
    {
        if (reporterUserId == Guid.Empty) throw new ArgumentException("An incident reporter is required.", nameof(reporterUserId));
        var incident = new OrderIncident(Id, reporterUserId, category, description, DateTimeOffset.UtcNow, commandId);
        _incidents.Add(incident);
        RecordActivity(reporterUserId, $"Incident reported: {incident.Category}", incident.ReportedAtUtc);
        return incident;
    }

    public bool ResolveIncident(Guid incidentId, Guid resolverUserId, string resolution)
    {
        var incident = _incidents.SingleOrDefault(item => item.Id == incidentId)
            ?? throw new KeyNotFoundException("Incident was not found.");
        var changed = incident.Resolve(resolverUserId, resolution, DateTimeOffset.UtcNow);
        if (changed) RecordActivity(resolverUserId, "Incident resolved", incident.ResolvedAtUtc!.Value);
        return changed;
    }

    public bool MarkRefunded(Guid reviewerUserId, decimal amount, string reference, string? note)
    {
        var changed = (RefundCase ?? throw new InvalidOperationException("Order has no refund follow-up."))
            .MarkRefunded(reviewerUserId, amount, reference, note, DateTimeOffset.UtcNow, Total);
        if (changed) RecordActivity(reviewerUserId, "Manual refund marked complete", RefundCase!.ReviewedAtUtc!.Value);
        return changed;
    }

    public bool MarkRefundNotRequired(Guid reviewerUserId, string reason)
    {
        var changed = (RefundCase ?? throw new InvalidOperationException("Order has no refund follow-up."))
            .MarkNotRequired(reviewerUserId, reason, DateTimeOffset.UtcNow);
        if (changed) RecordActivity(reviewerUserId, "Manual refund reviewed: not required", RefundCase!.ReviewedAtUtc!.Value);
        return changed;
    }

    public bool BeginPreparation(Guid businessUserId)
    {
        EnsureActor(businessUserId);
        if (HasRecordedChange(OrderStatus.Preparing, businessUserId, "Preparation started")) return false;
        EnsureStatus(OrderStatus.Confirmed);
        ChangeStatus(OrderStatus.Preparing, businessUserId, "Preparation started", DateTimeOffset.UtcNow);
        return true;
    }

    public bool MarkReadyForPickup(Guid businessUserId)
    {
        EnsureActor(businessUserId);
        if (HasRecordedChange(OrderStatus.ReadyForPickup, businessUserId, "Ready for pickup")) return false;
        EnsureStatus(OrderStatus.Preparing);
        ChangeStatus(OrderStatus.ReadyForPickup, businessUserId, "Ready for pickup", DateTimeOffset.UtcNow);
        return true;
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

    public void ApplyLineOptions(Guid productId, IEnumerable<(Guid OptionId, string Name, decimal PriceDelta)> options)
    {
        EnsureStatus(OrderStatus.PendingPayment);
        var line = _items.SingleOrDefault(item => item.ProductId == productId)
            ?? throw new ArgumentException("Product is not part of this order.", nameof(productId));
        line.SetOptions(options);
        Total = decimal.Round(_items.Sum(item => item.LineTotal) + DeliveryFee, 2, MidpointRounding.AwayFromZero);
    }

    public bool MarkPickedUp(Guid courierUserId, string submittedCodeHash)
    {
        EnsureCourier(courierUserId);
        if (Status is OrderStatus.PickedUp or OrderStatus.InTransit or OrderStatus.Delivered)
        {
            EnsureCode(PickupCodeHash, submittedCodeHash, "Pickup code is invalid.");
            return false;
        }
        EnsureStatus(OrderStatus.AssignedToDelivery);
        EnsureCode(PickupCodeHash, submittedCodeHash, "Pickup code is invalid.");
        var now = DateTimeOffset.UtcNow;
        CourierPaymentReceivedAtUtc = now;
        CourierPaymentReceivedByUserId = courierUserId;
        ChangeStatus(OrderStatus.PickedUp, courierUserId, "Courier confirms business paid at pickup", now);
        return true;
    }

    public bool BeginDelivery(Guid courierUserId, string contactChannel)
    {
        EnsureCourier(courierUserId);
        var normalized = contactChannel?.Trim().ToLowerInvariant();
        if (normalized is not ("whatsapp" or "call")) throw new ArgumentException("Contact channel must be whatsapp or call.", nameof(contactChannel));
        var reason = $"Delivery started; contact via {normalized}";
        if (HasRecordedChange(OrderStatus.InTransit, courierUserId, reason)) return false;
        EnsureStatus(OrderStatus.PickedUp);
        ContactChannel = normalized;
        DeliveryStartedAtUtc = DateTimeOffset.UtcNow;
        ChangeStatus(OrderStatus.InTransit, courierUserId, reason, DeliveryStartedAtUtc.Value);
        return true;
    }

    public bool MarkDelivered(Guid courierUserId, string submittedCodeHash)
    {
        EnsureCourier(courierUserId);
        if (Status == OrderStatus.Delivered)
        {
            EnsureCode(DeliveryCodeHash, submittedCodeHash, "Delivery code is invalid.");
            return false;
        }
        EnsureStatus(OrderStatus.InTransit);
        EnsureCode(DeliveryCodeHash, submittedCodeHash, "Delivery code is invalid.");
        DeliveredAtUtc = DateTimeOffset.UtcNow;
        ChangeStatus(OrderStatus.Delivered, courierUserId, "Customer confirmed receipt", DeliveredAtUtc.Value);
        return true;
    }

    private void EnsureCourier(Guid courierUserId)
    {
        if (courierUserId == Guid.Empty || CourierUserId != courierUserId)
            throw new InvalidOperationException("Only the assigned courier can perform this operation.");
    }

    private bool HasRecordedChange(OrderStatus status, Guid actorUserId, string reason) =>
        _statusHistory.Any(change => change.Status == status && change.ActorUserId == actorUserId && change.Reason == reason);

    private bool Cancel(Guid actorUserId, string reason, OrderStatus[] allowedStatuses)
    {
        var normalizedReason = string.IsNullOrWhiteSpace(reason) ? string.Empty : reason.Trim();
        if (Status == OrderStatus.Cancelled && CancelledByUserId == actorUserId && CancellationReason == normalizedReason)
            return false;
        if (normalizedReason.Length is < 3 or > 500) throw new ArgumentException("Cancellation reason must be between 3 and 500 characters.", nameof(reason));
        if (!allowedStatuses.Contains(Status)) throw new InvalidOperationException("Order cannot be cancelled in its current state.");
        var now = DateTimeOffset.UtcNow;
        CancelledAtUtc = now;
        CancelledByUserId = actorUserId;
        CancellationReason = normalizedReason;
        ChangeStatus(OrderStatus.Cancelled, actorUserId, normalizedReason, now);
        EnsureRefundFollowUp(actorUserId);
        return true;
    }

    private void EnsureRefundFollowUp(Guid requestedByUserId)
    {
        if (_transferReviews.Any(review => review.Decision == TransferDecision.Approved || review.Decision is null))
            RefundCase ??= ManualRefundCase.Create(Id, Total, requestedByUserId, DateTimeOffset.UtcNow);
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

    private void RecordActivity(Guid actorUserId, string reason, DateTimeOffset changedAtUtc)
    {
        Version++;
        _statusHistory.Add(new OrderStatusChange(Guid.NewGuid(), actorUserId, Status, changedAtUtc, reason));
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
    private readonly List<OrderLineOption> _options = [];
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
    public IReadOnlyCollection<OrderLineOption> Options => _options.AsReadOnly();

    internal void SetOptions(IEnumerable<(Guid OptionId, string Name, decimal PriceDelta)> options)
    {
        var selections = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));
        if (selections.Select(option => option.OptionId).Distinct().Count() != selections.Length)
            throw new ArgumentException("An option may only be selected once.", nameof(options));
        var previousOptionsTotal = _options.Sum(option => option.PriceDelta);
        _options.Clear();
        foreach (var selection in selections)
            _options.Add(new OrderLineOption(selection.OptionId, selection.Name, selection.PriceDelta));
        UnitPrice = decimal.Round(UnitPrice - previousOptionsTotal + selections.Sum(option => option.PriceDelta), 2, MidpointRounding.AwayFromZero);
        LineTotal = decimal.Round(UnitPrice * Quantity, 2, MidpointRounding.AwayFromZero);
    }
}

public sealed class OrderLineOption
{
    private OrderLineOption() { }
    internal OrderLineOption(Guid productOptionId, string name, decimal priceDelta)
    {
        if (productOptionId == Guid.Empty) throw new ArgumentException("A catalog option is required.", nameof(productOptionId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An option name is required.", nameof(name));
        if (priceDelta < 0) throw new ArgumentOutOfRangeException(nameof(priceDelta));
        Id = Guid.NewGuid();
        ProductOptionId = productOptionId;
        Name = name.Trim();
        PriceDelta = decimal.Round(priceDelta, 2, MidpointRounding.AwayFromZero);
    }
    public Guid Id { get; private set; }
    public Guid ProductOptionId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PriceDelta { get; private set; }
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
