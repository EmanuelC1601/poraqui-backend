using Poraqui.Domain.Orders;
using Poraqui.Application.Commerce;
using System.Security.Cryptography;
using System.Text;

namespace Poraqui.Application.Orders;

public interface IOrderWorkflowRepository
{
    Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetOrdersForBusinessAsync(Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<Order>> GetOrdersForCourierAsync(Guid courierId, CancellationToken cancellationToken);
    Task<string?> GetBusinessAddressAsync(Guid businessId, CancellationToken cancellationToken);
    Task<bool> BusinessBelongsToAsync(Guid businessId, Guid ownerUserId, CancellationToken cancellationToken);
    Task<string?> GetCustomerPhoneAsync(Guid customerUserId, CancellationToken cancellationToken);
    Task<OfflineDeliverySyncRecord?> GetOfflineDeliveryCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken);
    Task<OrderIncident?> GetIncidentCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken);
    Task<TransferReview?> GetTransferEvidenceCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken);
    Task<CourierAssignmentCommand?> GetCourierAssignmentCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ExternalDeliveryOffer>> CancelOpenExternalOffersForOrderAsync(Guid orderId, CancellationToken cancellationToken);
    Task<bool> SaveOfflineDeliveryCommandAsync(OfflineDeliverySyncRecord command, CancellationToken cancellationToken);
    Task<bool> SaveIncidentCommandAsync(Guid actorUserId, Guid commandId, CancellationToken cancellationToken);
    Task<bool> SaveTransferEvidenceCommandAsync(Order order, CancellationToken cancellationToken);
    Task<bool> SaveCourierAssignmentCommandAsync(Order order, CourierAssignmentCommand command, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITransferEvidenceStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);
    Task<(Stream Content, string ContentType)?> OpenAsync(string reference, CancellationToken cancellationToken);
    Task DeleteAsync(string reference, CancellationToken cancellationToken);
}

public sealed record OrderWorkflowLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal,
    IReadOnlyList<SelectedOrderOptionView> Options);
public sealed record OrderWorkflowHistory(Guid ActorUserId, string Status, DateTimeOffset ChangedAtUtc, string Reason);
public sealed record OrderWorkflowView(Guid Id, string OrderNumber, Guid CustomerUserId, Guid BusinessId, string BusinessPickupAddress, Guid ZoneId,
    Guid? CourierUserId, string DeliveryAddress, decimal Subtotal, decimal DeliveryFee, decimal Total,
    decimal? CourierPaymentAmount, bool CourierPaymentReceived, string Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeliveryStartedAtUtc, DateTimeOffset? DeliveredAtUtc, IReadOnlyList<OrderWorkflowLine> Items,
    IReadOnlyList<OrderWorkflowHistory> History, TransferInstructionsView? TransferInstructions, string? RefundStatus = null);
public sealed record CourierAssignment(string PickupCode, decimal PaymentAmount, OrderWorkflowView Order);
public sealed record CustomerContact(string? PhoneNumber, string WhatsAppDialString, string Channel);
public sealed record OfflineDeliverySyncResult(Guid CommandId, Guid OrderId, string Status, DateTimeOffset AcceptedAtUtc, bool Replayed);
public sealed record OrderIncidentView(Guid Id, Guid ReporterUserId, string Category, string Description,
    string Status, DateTimeOffset ReportedAtUtc, Guid? ResolverUserId, string? Resolution, DateTimeOffset? ResolvedAtUtc);
public sealed record ManualRefundView(Guid Id, Guid OrderId, decimal RequestedAmount, string Status, decimal? RefundedAmount,
    string? Reference, string? Note, Guid RequestedByUserId, DateTimeOffset CreatedAtUtc,
    Guid? ReviewedByUserId, DateTimeOffset? ReviewedAtUtc);
public sealed record OrderRealtimeSnapshot(Guid OrderId, string Status, long Version, DateTimeOffset ChangedAtUtc);
public sealed record OrderRealtimeSubscription(OrderRealtimeSnapshot Snapshot, string Audience);

public sealed class OrderWorkflowService(IOrderWorkflowRepository repository, IOrderChangePublisher publisher,
    IOrderCommandSecretProtector secretProtector, IExternalOfferPublisher offerPublisher)
{
    public async Task<bool> HasTransferEvidenceReplayAsync(Guid orderId, Guid customerId, Guid commandId,
        string contentHash, CancellationToken cancellationToken)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        var order = await RequiredOrder(orderId, cancellationToken);
        if (order.CustomerUserId != customerId) throw new OrderResourceForbiddenException();
        var existing = await repository.GetTransferEvidenceCommandAsync(customerId, commandId, cancellationToken);
        if (existing is null) return false;
        EnsureMatchingTransferEvidence(existing, orderId, contentHash);
        return true;
    }

    public async Task<bool> SubmitTransferEvidenceAsync(Guid orderId, Guid customerId, Guid commandId,
        string contentHash, string privateEvidenceReference, CancellationToken cancellationToken)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        var order = await RequiredOrder(orderId, cancellationToken);
        if (order.CustomerUserId != customerId) throw new OrderResourceForbiddenException();
        var existing = await repository.GetTransferEvidenceCommandAsync(customerId, commandId, cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingTransferEvidence(existing, orderId, contentHash);
            return false;
        }
        order.SubmitTransferEvidence(privateEvidenceReference, customerId, commandId, contentHash);
        var saved = await repository.SaveTransferEvidenceCommandAsync(order, cancellationToken);
        if (!saved)
        {
            existing = await repository.GetTransferEvidenceCommandAsync(customerId, commandId, cancellationToken)
                ?? throw new InvalidOperationException("The transfer evidence command could not be recovered after a concurrent retry.");
            EnsureMatchingTransferEvidence(existing, orderId, contentHash);
            return false;
        }
        await PublishChange(order, cancellationToken);
        return true;
    }

    private static void EnsureMatchingTransferEvidence(TransferReview existing, Guid orderId, string contentHash)
    {
        if (existing.OrderId != orderId ||
            !string.Equals(existing.ContentHash, contentHash, StringComparison.Ordinal))
            throw new TransferEvidenceCommandConflictException();
    }

    public async Task<IReadOnlyList<OrderWorkflowView>> GetBusinessOrdersAsync(Guid businessId, Guid ownerId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        await EnsureBusinessAccess(businessId, ownerId, isAdmin, cancellationToken);
        var address = await repository.GetBusinessAddressAsync(businessId, cancellationToken) ?? string.Empty;
        return (await repository.GetOrdersForBusinessAsync(businessId, cancellationToken))
            .Select(order => ToView(order, address)).ToArray();
    }

    public async Task<OrderWorkflowView?> GetForCustomerAsync(Guid orderId, Guid customerId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        if (order.CustomerUserId != customerId) throw new OrderResourceForbiddenException();
        var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
        return ToView(order, address) with
        {
            CourierPaymentAmount = null,
            CourierPaymentReceived = false,
            TransferInstructions = order.TransferRecipientName is null ? null : new TransferInstructionsView(
                order.TransferRecipientName, order.TransferInstitution!, order.TransferAccountReference!, order.TransferInstructions),
            RefundStatus = order.RefundCase?.Status.ToString()
        };
    }

    public async Task<OrderWorkflowView?> GetForBusinessAsync(Guid orderId, Guid actorId, bool isAdmin, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        await EnsureBusinessAccess(order.BusinessId, actorId, isAdmin, cancellationToken);
        var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
        return ToView(order, address);
    }

    public async Task<OrderWorkflowView?> GetForCourierAsync(Guid orderId, Guid courierId, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        EnsureCourier(order, courierId);
        var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
        return ToView(order, address);
    }

    public async Task<OrderWorkflowView?> CancelForCustomerAsync(Guid orderId, Guid customerId, string reason,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        if (order.CustomerUserId != customerId) throw new OrderResourceForbiddenException();
        if (order.CancelByCustomer(customerId, reason))
        {
            await repository.SaveChangesAsync(cancellationToken);
            await PublishChange(order, cancellationToken);
        }
        return ToView(order, await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty);
    }

    public async Task<OrderWorkflowView?> CancelForBusinessAsync(Guid businessId, Guid orderId, Guid actorId,
        bool isAdmin, string reason, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null || order.BusinessId != businessId) return null;
        await EnsureBusinessAccess(businessId, actorId, isAdmin, cancellationToken);
        if (order.CancelByBusiness(actorId, reason))
        {
            var cancelledOffers = await repository.CancelOpenExternalOffersForOrderAsync(orderId, cancellationToken);
            await repository.SaveChangesAsync(cancellationToken);
            foreach (var offer in cancelledOffers)
                await offerPublisher.PublishChangedAsync(offer.CourierUserId,
                    new ExternalOfferChangedNotice(offer.Id, offer.OrderId, offer.Status.ToString()), cancellationToken);
            await PublishChange(order, cancellationToken);
        }
        return ToView(order, await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty);
    }

    public async Task<IReadOnlyList<OrderIncidentView>> GetIncidentsAsync(Guid orderId, Guid actorId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureOrderParticipant(order, actorId, isAdmin, cancellationToken);
        return order.Incidents.OrderBy(incident => incident.ReportedAtUtc).Select(ToView).ToArray();
    }

    public async Task<OrderIncidentView> ReportIncidentAsync(Guid orderId, Guid actorId, bool isAdmin,
        Guid commandId, string category, string description, CancellationToken cancellationToken)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureOrderParticipant(order, actorId, isAdmin, cancellationToken);
        var existing = await repository.GetIncidentCommandAsync(actorId, commandId, cancellationToken);
        if (existing is not null)
        {
            if (existing.OrderId != orderId || existing.Category != category.Trim() || existing.Description != description.Trim())
                throw new IncidentCommandConflictException();
            return ToView(existing);
        }
        var incident = order.AddIncident(actorId, category, description, commandId);
        if (!await repository.SaveIncidentCommandAsync(actorId, commandId, cancellationToken))
        {
            existing = await repository.GetIncidentCommandAsync(actorId, commandId, cancellationToken)
                ?? throw new InvalidOperationException("The incident command could not be recovered after a concurrent retry.");
            EnsureMatchingIncident(existing, orderId, category, description);
            return ToView(existing);
        }
        await PublishChange(order, cancellationToken);
        return ToView(incident);
    }

    private static void EnsureMatchingIncident(OrderIncident existing, Guid orderId, string category, string description)
    {
        if (existing.OrderId != orderId || existing.Category != category.Trim() || existing.Description != description.Trim())
            throw new IncidentCommandConflictException();
    }

    public async Task<OrderIncidentView?> ResolveIncidentAsync(Guid businessId, Guid orderId, Guid incidentId,
        Guid actorId, bool isAdmin, string resolution, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null || order.BusinessId != businessId) return null;
        await EnsureBusinessAccess(businessId, actorId, isAdmin, cancellationToken);
        var changed = order.ResolveIncident(incidentId, actorId, resolution);
        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
            await PublishChange(order, cancellationToken);
        }
        return order.Incidents.SingleOrDefault(item => item.Id == incidentId) is { } incident ? ToView(incident) : null;
    }

    public async Task<ManualRefundView?> GetRefundCaseAsync(Guid businessId, Guid orderId, Guid actorId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null || order.BusinessId != businessId) return null;
        await EnsureBusinessAccess(businessId, actorId, isAdmin, cancellationToken);
        return order.RefundCase is null ? null : ToView(order.RefundCase);
    }

    public async Task<ManualRefundView?> ReviewRefundCaseAsync(Guid businessId, Guid orderId, Guid actorId, bool isAdmin,
        bool refunded, decimal? amount, string? reference, string? note, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null || order.BusinessId != businessId) return null;
        await EnsureBusinessAccess(businessId, actorId, isAdmin, cancellationToken);
        var changed = refunded
            ? order.MarkRefunded(actorId, amount ?? throw new ArgumentException("Refund amount is required.", nameof(amount)),
                reference ?? throw new ArgumentException("Refund reference is required.", nameof(reference)), note)
            : order.MarkRefundNotRequired(actorId, note ?? throw new ArgumentException("A review reason is required.", nameof(note)));
        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
            await PublishChange(order, cancellationToken);
        }
        return ToView(order.RefundCase!);
    }

    private async Task EnsureOrderParticipant(Order order, Guid actorId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && order.CustomerUserId != actorId && order.CourierUserId != actorId &&
            !await repository.BusinessBelongsToAsync(order.BusinessId, actorId, cancellationToken))
            throw new OrderResourceForbiddenException();
    }

    public async Task<OrderRealtimeSubscription> GetRealtimeSnapshotAsync(Guid orderId, Guid actorId, bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        var audience = isAdmin ? "admin" : order.CustomerUserId == actorId ? "customer" :
            order.CourierUserId == actorId ? "courier" :
            await repository.BusinessBelongsToAsync(order.BusinessId, actorId, cancellationToken) ? "business" : null;
        if (audience is null) throw new OrderResourceForbiddenException();
        return new OrderRealtimeSubscription(new OrderRealtimeSnapshot(order.Id, order.Status.ToString(), order.Version,
            order.StatusHistory.OrderByDescending(entry => entry.ChangedAtUtc).First().ChangedAtUtc), audience);
    }

    public async Task<IReadOnlyList<OrderWorkflowView>> GetOrdersForCourierAsync(Guid courierId, CancellationToken cancellationToken)
    {
        var orders = await repository.GetOrdersForCourierAsync(courierId, cancellationToken);
        var result = new List<OrderWorkflowView>(orders.Count);
        foreach (var order in orders)
        {
            var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
            result.Add(ToView(order, address));
        }
        return result;
    }

    public async Task<string?> GetTransferEvidenceForBusinessAsync(Guid orderId, Guid actorId, bool isAdmin, CancellationToken cancellationToken)
    {
        var order = await repository.GetOrderAsync(orderId, cancellationToken);
        if (order is null) return null;
        await EnsureBusinessAccess(order.BusinessId, actorId, isAdmin, cancellationToken);
        return order.TransferReviews.OrderByDescending(review => review.SubmittedAtUtc)
            .Select(review => review.EvidenceReference).FirstOrDefault();
    }

    public async Task ReviewPaymentAsync(Guid orderId, Guid actorId, bool approved, string? note, bool isAdmin,
        CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, actorId, isAdmin, cancellationToken);
        if (!order.ReviewTransfer(actorId, approved, note)) return;
        await repository.SaveChangesAsync(cancellationToken);
        await PublishChange(order, cancellationToken);
    }

    public async Task DecideBusinessAsync(Guid orderId, Guid actorId, bool accept, string? reason,
        CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, actorId, false, cancellationToken);
        var changed = accept
            ? order.AcceptByBusiness(actorId)
            : order.RejectByBusiness(actorId, reason ?? string.Empty);
        if (!changed) return;
        await repository.SaveChangesAsync(cancellationToken);
        await PublishChange(order, cancellationToken);
    }

    public async Task ChangePreparationAsync(Guid orderId, Guid actorId, bool ready, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, actorId, false, cancellationToken);
        var changed = ready ? order.MarkReadyForPickup(actorId) : order.BeginPreparation(actorId);
        if (!changed) return;
        await repository.SaveChangesAsync(cancellationToken);
        await PublishChange(order, cancellationToken);
    }

    public async Task<CourierAssignment> AssignCourierAsync(Guid orderId, Guid businessUserId, Guid courierId,
        decimal courierPaymentAmount, Guid commandId, CancellationToken cancellationToken)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        if (courierPaymentAmount < 0) throw new ArgumentOutOfRangeException(nameof(courierPaymentAmount));
        courierPaymentAmount = decimal.Round(courierPaymentAmount, 2, MidpointRounding.AwayFromZero);
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, businessUserId, false, cancellationToken);
        var requestHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
            $"{orderId:D}|{courierId:D}|{courierPaymentAmount.ToString("0.00", System.Globalization.CultureInfo.InvariantCulture)}")));
        var existing = await repository.GetCourierAssignmentCommandAsync(businessUserId, commandId, cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingAssignment(existing, orderId, requestHash);
            return await ReplayCourierAssignment(existing, cancellationToken);
        }
        var pickupCode = OrderAccessCode.Generate();
        order.AssignCourier(businessUserId, courierId, OrderAccessCode.Hash(pickupCode), courierPaymentAmount);
        var command = new CourierAssignmentCommand(businessUserId, commandId, orderId, courierId, courierPaymentAmount,
            requestHash, secretProtector.Protect(pickupCode), DateTimeOffset.UtcNow);
        if (!await repository.SaveCourierAssignmentCommandAsync(order, command, cancellationToken))
        {
            existing = await repository.GetCourierAssignmentCommandAsync(businessUserId, commandId, cancellationToken)
                ?? throw new InvalidOperationException("Courier assignment retry could not be recovered.");
            EnsureMatchingAssignment(existing, orderId, requestHash);
            return await ReplayCourierAssignment(existing, cancellationToken);
        }
        await PublishChange(order, cancellationToken);
        var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
        return new CourierAssignment(pickupCode, order.CourierPaymentAmount!.Value, ToView(order, address));
    }

    private static void EnsureMatchingAssignment(CourierAssignmentCommand command, Guid orderId, string requestHash)
    {
        if (command.OrderId != orderId || !string.Equals(command.RequestHash, requestHash, StringComparison.Ordinal))
            throw new CourierAssignmentCommandConflictException();
    }

    private async Task<CourierAssignment> ReplayCourierAssignment(CourierAssignmentCommand command, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(command.OrderId, cancellationToken);
        if (order.CourierUserId != command.CourierUserId || order.CourierPaymentAmount != command.CourierPaymentAmount)
            throw new CourierAssignmentCommandConflictException();
        var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
        return new CourierAssignment(secretProtector.Unprotect(command.ProtectedPickupCode), command.CourierPaymentAmount,
            ToView(order, address));
    }

    public async Task MarkPickedUpAsync(Guid orderId, Guid courierId, string pickupCode, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        if (!order.MarkPickedUp(courierId, OrderAccessCode.Hash(pickupCode))) return;
        await repository.SaveChangesAsync(cancellationToken);
        await PublishChange(order, cancellationToken);
    }

    public async Task<CustomerContact> BeginDeliveryAsync(Guid orderId, Guid courierId, string channel, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        var changed = order.BeginDelivery(courierId, channel);
        var phone = await repository.GetCustomerPhoneAsync(order.CustomerUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Customer has no phone number available for delivery contact.");
        if (changed)
        {
            await repository.SaveChangesAsync(cancellationToken);
            await PublishChange(order, cancellationToken);
        }
        return new CustomerContact(phone, NormalizePhone(phone), channel.ToLowerInvariant());
    }

    public async Task MarkDeliveredAsync(Guid orderId, Guid courierId, string deliveryCode, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        if (!order.MarkDelivered(courierId, OrderAccessCode.Hash(deliveryCode))) return;
        await repository.SaveChangesAsync(cancellationToken);
        await PublishChange(order, cancellationToken);
    }

    public async Task<OfflineDeliverySyncResult> SyncDeliveryConfirmationAsync(Guid orderId, Guid courierId, Guid commandId,
        string deliveryCode, DateTimeOffset clientCapturedAtUtc, CancellationToken cancellationToken)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("Command id is required.", nameof(commandId));
        if (clientCapturedAtUtc == default) throw new ArgumentException("Client capture time is required.", nameof(clientCapturedAtUtc));
        var payloadHash = OrderAccessCode.Hash($"{orderId:N}|{deliveryCode}|{clientCapturedAtUtc:O}");
        var existing = await repository.GetOfflineDeliveryCommandAsync(courierId, commandId, cancellationToken);
        if (existing is not null)
        {
            EnsureMatchingOfflineDeliveryCommand(existing, orderId, payloadHash);
            return new OfflineDeliverySyncResult(commandId, orderId, OrderStatus.Delivered.ToString(), existing.ServerAcceptedAtUtc, true);
        }

        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        var acceptedAt = DateTimeOffset.UtcNow;
        order.MarkDelivered(courierId, OrderAccessCode.Hash(deliveryCode));
        var command = new OfflineDeliverySyncRecord(commandId, courierId, orderId, payloadHash,
            clientCapturedAtUtc, acceptedAt);
        if (!await repository.SaveOfflineDeliveryCommandAsync(command, cancellationToken))
        {
            existing = await repository.GetOfflineDeliveryCommandAsync(courierId, commandId, cancellationToken)
                ?? throw new InvalidOperationException("The offline delivery command could not be recovered after a concurrent retry.");
            EnsureMatchingOfflineDeliveryCommand(existing, orderId, payloadHash);
            return new OfflineDeliverySyncResult(commandId, orderId, OrderStatus.Delivered.ToString(), existing.ServerAcceptedAtUtc, true);
        }
        await PublishChange(order, cancellationToken);
        return new OfflineDeliverySyncResult(commandId, orderId, OrderStatus.Delivered.ToString(), acceptedAt, false);
    }

    private static void EnsureMatchingOfflineDeliveryCommand(OfflineDeliverySyncRecord existing, Guid orderId, string payloadHash)
    {
        if (existing.OrderId != orderId || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(existing.PayloadHash), Convert.FromHexString(payloadHash)))
            throw new OfflineDeliveryCommandConflictException();
    }

    private async Task<Order> RequiredOrder(Guid id, CancellationToken cancellationToken) =>
        await repository.GetOrderAsync(id, cancellationToken) ?? throw new KeyNotFoundException("Order was not found.");

    private async Task EnsureBusinessAccess(Guid businessId, Guid actorId, bool isAdmin, CancellationToken cancellationToken)
    {
        if (!isAdmin && !await repository.BusinessBelongsToAsync(businessId, actorId, cancellationToken))
            throw new OrderResourceForbiddenException();
    }

    private static void EnsureCourier(Order order, Guid courierId)
    {
        if (order.CourierUserId != courierId) throw new OrderResourceForbiddenException();
    }

    private static string NormalizePhone(string? phone) => new((phone ?? string.Empty).Where(char.IsDigit).ToArray());

    private Task PublishChange(Order order, CancellationToken cancellationToken)
    {
        var change = order.StatusHistory.OrderByDescending(entry => entry.ChangedAtUtc).First();
        return publisher.PublishAsync(new OrderChangeNotice(order.Id, order.BusinessId, order.CustomerUserId,
            order.CourierUserId, order.Status.ToString(), order.Version, change.ChangedAtUtc), cancellationToken);
    }

    private static OrderWorkflowView ToView(Order order, string businessAddress) => new(order.Id, order.OrderNumber, order.CustomerUserId,
        order.BusinessId, businessAddress, order.ZoneId, order.CourierUserId, order.DeliveryAddress, order.Items.Sum(item => item.LineTotal),
        order.DeliveryFee, order.Total, order.CourierPaymentAmount, order.CourierPaymentReceivedAtUtc is not null,
        order.Status.ToString(), order.CreatedAtUtc, order.DeliveryStartedAtUtc, order.DeliveredAtUtc,
        order.Items.Select(item => new OrderWorkflowLine(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal,
            item.Options.Select(option => new SelectedOrderOptionView(option.ProductOptionId, option.Name, option.PriceDelta)).ToArray())).ToArray(),
        order.StatusHistory.OrderBy(entry => entry.ChangedAtUtc)
            .Select(entry => new OrderWorkflowHistory(entry.ActorUserId, entry.Status.ToString(), entry.ChangedAtUtc, entry.Reason)).ToArray(), null,
        order.RefundCase?.Status.ToString());

    private static OrderIncidentView ToView(OrderIncident incident) => new(incident.Id, incident.ReporterUserId,
        incident.Category, incident.Description, incident.Status.ToString(), incident.ReportedAtUtc,
        incident.ResolverUserId, incident.Resolution, incident.ResolvedAtUtc);

    private static ManualRefundView ToView(ManualRefundCase refund) => new(refund.Id, refund.OrderId,
        refund.RequestedAmount, refund.Status.ToString(), refund.RefundedAmount, refund.Reference, refund.Note,
        refund.RequestedByUserId, refund.CreatedAtUtc, refund.ReviewedByUserId, refund.ReviewedAtUtc);
}

public sealed class OrderResourceForbiddenException : Exception { }
public sealed class OfflineDeliveryCommandConflictException : Exception { }
public sealed class IncidentCommandConflictException : Exception { }
public sealed class TransferEvidenceCommandConflictException : Exception { }
public sealed class CourierAssignmentCommandConflictException : Exception { }
