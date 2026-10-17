namespace Poraqui.Domain.Orders;

public sealed class OfflineDeliverySyncRecord
{
    private OfflineDeliverySyncRecord() { }

    public OfflineDeliverySyncRecord(Guid id, Guid actorUserId, Guid orderId, string payloadHash,
        DateTimeOffset clientCapturedAtUtc, DateTimeOffset serverAcceptedAtUtc)
    {
        if (id == Guid.Empty || actorUserId == Guid.Empty || orderId == Guid.Empty)
            throw new ArgumentException("Command, actor and order identifiers are required.");
        if (string.IsNullOrWhiteSpace(payloadHash)) throw new ArgumentException("A payload hash is required.", nameof(payloadHash));
        if (clientCapturedAtUtc == default || serverAcceptedAtUtc == default)
            throw new ArgumentException("Client and server timestamps are required.");
        Id = id;
        ActorUserId = actorUserId;
        OrderId = orderId;
        PayloadHash = payloadHash;
        ClientCapturedAtUtc = clientCapturedAtUtc;
        ServerAcceptedAtUtc = serverAcceptedAtUtc;
    }

    public Guid Id { get; private set; }
    public Guid ActorUserId { get; private set; }
    public Guid OrderId { get; private set; }
    public string PayloadHash { get; private set; } = string.Empty;
    public DateTimeOffset ClientCapturedAtUtc { get; private set; }
    public DateTimeOffset ServerAcceptedAtUtc { get; private set; }
}
