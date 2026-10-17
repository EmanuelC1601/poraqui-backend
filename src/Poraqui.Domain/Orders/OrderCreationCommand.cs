namespace Poraqui.Domain.Orders;

public sealed class OrderCreationCommand
{
    private OrderCreationCommand() { }

    public OrderCreationCommand(Guid customerUserId, Guid commandId, Guid orderId, string requestHash,
        string protectedDeliveryCode, DateTimeOffset createdAtUtc)
    {
        if (customerUserId == Guid.Empty) throw new ArgumentException("A customer is required.", nameof(customerUserId));
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        if (orderId == Guid.Empty) throw new ArgumentException("An order is required.", nameof(orderId));
        CustomerUserId = customerUserId;
        CommandId = commandId;
        OrderId = orderId;
        RequestHash = requestHash;
        ProtectedDeliveryCode = protectedDeliveryCode;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid CustomerUserId { get; private set; }
    public Guid CommandId { get; private set; }
    public Guid OrderId { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public string ProtectedDeliveryCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
