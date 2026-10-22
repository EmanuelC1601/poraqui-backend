using Poraqui.Application.Orders;
using Poraqui.Domain.Orders;

namespace Poraqui.Domain.Tests;

public sealed class ExternalCourierServiceTests
{
    [Fact]
    public async Task Expiration_sweep_persists_before_publishing_expired_notice()
    {
        var courierId = Guid.NewGuid();
        var offer = new ExternalDeliveryOffer(Guid.NewGuid(), courierId, Guid.NewGuid(), "Origen", "Destino",
            25m, null, DateTimeOffset.UtcNow.AddMinutes(1));
        offer.Expire(DateTimeOffset.UtcNow.AddMinutes(2));
        Assert.Equal(ExternalDeliveryOfferStatus.Expired, offer.Status);

        var repository = new ExpirationRepository(offer);
        var publisher = new PersistenceCheckingOfferPublisher(repository);
        var service = new ExternalCourierService(repository, new NullOrderChangePublisher(), publisher,
            new TestSecretProtector());

        Assert.Equal(1, await service.ExpireDueOffersAsync(CancellationToken.None));
        Assert.True(publisher.Published);
    }

    private sealed class ExpirationRepository(ExternalDeliveryOffer dueOffer) : IExternalCourierRepository
    {
        public bool Persisted { get; private set; }

        public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) => Task.FromResult<Order?>(null);
        public Task<string?> GetBusinessAddressAsync(Guid businessId, CancellationToken cancellationToken) => Task.FromResult<string?>(null);
        public Task<bool> BusinessBelongsToAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<Guid>> GetAvailableCouriersAsync(Guid zoneId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Guid>>([]);
        public Task<IReadOnlySet<Guid>> GetCouriersWithActiveOffersAsync(Guid orderId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<Guid>>(new HashSet<Guid>());
        public Task<CourierAvailability?> GetAvailabilityAsync(Guid courierId, CancellationToken cancellationToken) =>
            Task.FromResult<CourierAvailability?>(null);
        public Task<bool> IsActiveZoneAsync(Guid zoneId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> IsExternalCourierAsync(Guid courierId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasDeliveryCourierRoleAsync(Guid courierId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<ExternalDeliveryOffer>> GetOffersAsync(Guid courierId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalDeliveryOffer>>([]);
        public Task<IReadOnlyList<ExternalDeliveryOffer>> GetOffersForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalDeliveryOffer>>([]);
        public Task<ExternalDeliveryOffer?> GetOfferAsync(Guid offerId, CancellationToken cancellationToken) =>
            Task.FromResult<ExternalDeliveryOffer?>(null);
        public Task<bool> HasAcceptedOfferAsync(Guid orderId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<IReadOnlyList<ExternalDeliveryOffer>> ExpireDueOffersAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalDeliveryOffer>>([dueOffer]);
        public Task<IReadOnlyList<ExternalDeliveryOffer>> ExpireDueOffersForOrderAsync(Guid orderId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ExternalDeliveryOffer>>([]);
        public void AddOffer(ExternalDeliveryOffer offer) { }
        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            Persisted = true;
            return Task.CompletedTask;
        }
    }

    private sealed class PersistenceCheckingOfferPublisher(ExpirationRepository repository) : IExternalOfferPublisher
    {
        public bool Published { get; private set; }

        public Task PublishAvailableAsync(Guid courierUserId, ExternalOfferNotice notice, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task PublishChangedAsync(Guid courierUserId, ExternalOfferChangedNotice notice, CancellationToken cancellationToken)
        {
            Assert.True(repository.Persisted, "An expiration notice must follow persisted offer state.");
            Assert.Equal("Expired", notice.Status);
            Published = true;
            return Task.CompletedTask;
        }
    }

    private sealed class TestSecretProtector : IExternalOfferSecretProtector
    {
        public string Protect(string secret) => secret;
        public string Unprotect(string protectedSecret) => protectedSecret;
    }
}
