namespace Poraqui.Application.Orders;

public sealed record ExternalOfferNotice(Guid OfferId, Guid OrderId);

public interface IExternalOfferPublisher
{
    Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken);
}

public sealed class NullExternalOfferPublisher : IExternalOfferPublisher
{
    public Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken) =>
        Task.CompletedTask;
}
