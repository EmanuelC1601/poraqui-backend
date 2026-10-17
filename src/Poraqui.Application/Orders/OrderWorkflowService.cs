using Poraqui.Application.Commerce;
using Poraqui.Domain.Orders;

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
    void AddOfflineDeliveryCommand(OfflineDeliverySyncRecord command);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface ITransferEvidenceStorage
{
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken cancellationToken);
    Task<(Stream Content, string ContentType)?> OpenAsync(string reference, CancellationToken cancellationToken);
    Task DeleteAsync(string reference, CancellationToken cancellationToken);
}

public sealed record OrderWorkflowLine(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);
public sealed record OrderWorkflowHistory(Guid ActorUserId, string Status, DateTimeOffset ChangedAtUtc, string Reason);
public sealed record OrderWorkflowView(Guid Id, string OrderNumber, Guid CustomerUserId, Guid BusinessId, string BusinessPickupAddress, Guid ZoneId,
    Guid? CourierUserId, string DeliveryAddress, decimal Subtotal, decimal DeliveryFee, decimal Total,
    decimal? CourierPaymentAmount, bool CourierPaymentReceived, string Status, DateTimeOffset CreatedAtUtc,
    DateTimeOffset? DeliveryStartedAtUtc, DateTimeOffset? DeliveredAtUtc, IReadOnlyList<OrderWorkflowLine> Items,
    IReadOnlyList<OrderWorkflowHistory> History, TransferInstructionsView? TransferInstructions);
public sealed record CourierAssignment(string PickupCode, decimal PaymentAmount, OrderWorkflowView Order);
public sealed record CustomerContact(string? PhoneNumber, string WhatsAppDialString, string Channel);
public sealed record OfflineDeliverySyncResult(Guid CommandId, Guid OrderId, string Status, DateTimeOffset AcceptedAtUtc, bool Replayed);

public sealed class OrderWorkflowService(IOrderWorkflowRepository repository)
{
    public async Task SubmitTransferEvidenceAsync(Guid orderId, Guid customerId, string privateEvidenceReference,
        CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        if (order.CustomerUserId != customerId) throw new OrderResourceForbiddenException();
        order.SubmitTransferEvidence(privateEvidenceReference, customerId);
        await repository.SaveChangesAsync(cancellationToken);
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
                order.TransferRecipientName, order.TransferInstitution!, order.TransferAccountReference!, order.TransferInstructions)
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
        order.ReviewTransfer(actorId, approved, note);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task DecideBusinessAsync(Guid orderId, Guid actorId, bool accept, string? reason,
        CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, actorId, false, cancellationToken);
        if (accept) order.AcceptByBusiness(actorId);
        else order.RejectByBusiness(actorId, reason ?? string.Empty);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task ChangePreparationAsync(Guid orderId, Guid actorId, bool ready, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, actorId, false, cancellationToken);
        if (ready) order.MarkReadyForPickup(actorId);
        else order.BeginPreparation(actorId);
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<CourierAssignment> AssignCourierAsync(Guid orderId, Guid businessUserId, Guid courierId,
        decimal courierPaymentAmount, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        await EnsureBusinessAccess(order.BusinessId, businessUserId, false, cancellationToken);
        var pickupCode = OrderAccessCode.Generate();
        order.AssignCourier(businessUserId, courierId, OrderAccessCode.Hash(pickupCode), courierPaymentAmount);
        await repository.SaveChangesAsync(cancellationToken);
        var address = await repository.GetBusinessAddressAsync(order.BusinessId, cancellationToken) ?? string.Empty;
        return new CourierAssignment(pickupCode, order.CourierPaymentAmount!.Value, ToView(order, address));
    }

    public async Task MarkPickedUpAsync(Guid orderId, Guid courierId, string pickupCode, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        order.MarkPickedUp(courierId, OrderAccessCode.Hash(pickupCode));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<CustomerContact> BeginDeliveryAsync(Guid orderId, Guid courierId, string channel, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        order.BeginDelivery(courierId, channel);
        var phone = await repository.GetCustomerPhoneAsync(order.CustomerUserId, cancellationToken);
        if (string.IsNullOrWhiteSpace(phone))
            throw new InvalidOperationException("Customer has no phone number available for delivery contact.");
        await repository.SaveChangesAsync(cancellationToken);
        return new CustomerContact(phone, NormalizePhone(phone), channel.ToLowerInvariant());
    }

    public async Task MarkDeliveredAsync(Guid orderId, Guid courierId, string deliveryCode, CancellationToken cancellationToken)
    {
        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        order.MarkDelivered(courierId, OrderAccessCode.Hash(deliveryCode));
        await repository.SaveChangesAsync(cancellationToken);
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
            if (existing.OrderId != orderId || !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    Convert.FromHexString(existing.PayloadHash), Convert.FromHexString(payloadHash)))
                throw new OfflineDeliveryCommandConflictException();
            return new OfflineDeliverySyncResult(commandId, orderId, OrderStatus.Delivered.ToString(), existing.ServerAcceptedAtUtc, true);
        }

        var order = await RequiredOrder(orderId, cancellationToken);
        EnsureCourier(order, courierId);
        var acceptedAt = DateTimeOffset.UtcNow;
        order.MarkDelivered(courierId, OrderAccessCode.Hash(deliveryCode));
        repository.AddOfflineDeliveryCommand(new OfflineDeliverySyncRecord(commandId, courierId, orderId, payloadHash,
            clientCapturedAtUtc, acceptedAt));
        await repository.SaveChangesAsync(cancellationToken);
        return new OfflineDeliverySyncResult(commandId, orderId, OrderStatus.Delivered.ToString(), acceptedAt, false);
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

    private static OrderWorkflowView ToView(Order order, string businessAddress) => new(order.Id, order.OrderNumber, order.CustomerUserId,
        order.BusinessId, businessAddress, order.ZoneId, order.CourierUserId, order.DeliveryAddress, order.Items.Sum(item => item.LineTotal),
        order.DeliveryFee, order.Total, order.CourierPaymentAmount, order.CourierPaymentReceivedAtUtc is not null,
        order.Status.ToString(), order.CreatedAtUtc, order.DeliveryStartedAtUtc, order.DeliveredAtUtc,
        order.Items.Select(item => new OrderWorkflowLine(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal)).ToArray(),
        order.StatusHistory.OrderBy(entry => entry.ChangedAtUtc)
            .Select(entry => new OrderWorkflowHistory(entry.ActorUserId, entry.Status.ToString(), entry.ChangedAtUtc, entry.Reason)).ToArray(), null);
}

public sealed class OrderResourceForbiddenException : Exception { }
public sealed class OfflineDeliveryCommandConflictException : Exception { }
