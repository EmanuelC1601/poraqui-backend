namespace Poraqui.Application.Orders;

public sealed record ExternalOfferNotice(Guid OfferId, Guid OrderId);
public sealed record ExternalOfferChangedNotice(Guid OfferId, Guid OrderId, string Status);

public interface IExternalOfferPublisher
{
    Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken);
    Task PublishChangedAsync(Guid courierUserId, ExternalOfferChangedNotice notice, CancellationToken cancellationToken);
}

public sealed class NullExternalOfferPublisher : IExternalOfferPublisher
{
    public Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task PublishChangedAsync(Guid courierUserId, ExternalOfferChangedNotice notice, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
