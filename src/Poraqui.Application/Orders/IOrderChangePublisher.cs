namespace Poraqui.Application.Orders;

public sealed record OrderChangeNotice(Guid OrderId, Guid BusinessId, Guid CustomerUserId, Guid? CourierUserId,
    string Status, long Version, DateTimeOffset ChangedAtUtc);

public interface IOrderChangePublisher
{
    Task PublishAsync(OrderChangeNotice notice, CancellationToken cancellationToken);
}

public sealed class NullOrderChangePublisher : IOrderChangePublisher
{
    public Task PublishAsync(OrderChangeNotice notice, CancellationToken cancellationToken) => Task.CompletedTask;
}
