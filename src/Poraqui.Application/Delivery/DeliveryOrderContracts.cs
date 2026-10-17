namespace Poraqui.Application.Delivery;

/// <summary>Read model exposed to the delivery module. This contract has no persistence assumptions.</summary>
public sealed record DeliveryOrderSummary(
    Guid OrderId,
    string OrderNumber,
    string BusinessName,
    string PickupAddress,
    string DeliveryAddress,
    string ZoneName,
    DateTimeOffset ReadyAtUtc,
    string Status);

public sealed record DeliveryOrderDetails(
    Guid OrderId,
    string OrderNumber,
    string BusinessName,
    string PickupAddress,
    string CustomerName,
    string CustomerPhone,
    string DeliveryAddress,
    string ZoneName,
    decimal DeliveryFee,
    DateTimeOffset? ReadyAtUtc,
    string Status);

/// <summary>Queries available work and order details for the delivery module.</summary>
public interface IDeliveryOrderQueries
{
    Task<IReadOnlyList<DeliveryOrderSummary>> GetAvailableOrdersAsync(CancellationToken cancellationToken = default);
    Task<DeliveryOrderDetails?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken = default);
}

/// <summary>Commands delivery operations may request. Implementations own authorization and state validation.</summary>
public interface IDeliveryOrderOperations
{
    Task<bool> AcceptAsync(Guid orderId, Guid deliveryUserId, CancellationToken cancellationToken = default);
    Task<bool> MarkPickedUpAsync(Guid orderId, Guid deliveryUserId, CancellationToken cancellationToken = default);
    Task<bool> MarkDeliveredAsync(Guid orderId, Guid deliveryUserId, CancellationToken cancellationToken = default);
}
