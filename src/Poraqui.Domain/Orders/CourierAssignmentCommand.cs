namespace Poraqui.Domain.Orders;

public sealed class CourierAssignmentCommand
{
    private CourierAssignmentCommand() { }

    public CourierAssignmentCommand(Guid businessUserId, Guid commandId, Guid orderId, Guid courierUserId,
        decimal courierPaymentAmount, string requestHash, string protectedPickupCode, DateTimeOffset createdAtUtc)
    {
        if (businessUserId == Guid.Empty) throw new ArgumentException("A business user is required.", nameof(businessUserId));
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        if (orderId == Guid.Empty) throw new ArgumentException("An order is required.", nameof(orderId));
        if (courierUserId == Guid.Empty) throw new ArgumentException("A courier is required.", nameof(courierUserId));
        BusinessUserId = businessUserId;
        CommandId = commandId;
        OrderId = orderId;
        CourierUserId = courierUserId;
        CourierPaymentAmount = decimal.Round(courierPaymentAmount, 2, MidpointRounding.AwayFromZero);
        RequestHash = requestHash;
        ProtectedPickupCode = protectedPickupCode;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid BusinessUserId { get; private set; }
    public Guid CommandId { get; private set; }
    public Guid OrderId { get; private set; }
    public Guid CourierUserId { get; private set; }
    public decimal CourierPaymentAmount { get; private set; }
    public string RequestHash { get; private set; } = string.Empty;
    public string ProtectedPickupCode { get; private set; } = string.Empty;
    public DateTimeOffset CreatedAtUtc { get; private set; }
}
