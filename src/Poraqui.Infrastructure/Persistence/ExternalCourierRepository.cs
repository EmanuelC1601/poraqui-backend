using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Orders;
using Poraqui.Domain.Orders;
using Poraqui.Infrastructure.Identity;

namespace Poraqui.Infrastructure.Persistence;

public sealed class ExternalCourierRepository(AppDbContext db) : IExternalCourierRepository
{
    public Task<Order?> GetOrderAsync(Guid orderId, CancellationToken cancellationToken) => db.Orders
        .Include(x => x.Items).Include(x => x.StatusHistory)
        .SingleOrDefaultAsync(x => x.Id == orderId, cancellationToken);
    public Task<string?> GetBusinessAddressAsync(Guid businessId, CancellationToken cancellationToken) =>
        db.Businesses.Where(x => x.Id == businessId).Select(x => x.Address).SingleOrDefaultAsync(cancellationToken);
    public Task<bool> BusinessBelongsToAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken) =>
        db.Businesses.AnyAsync(x => x.Id == businessId && x.OwnerUserId == ownerId, cancellationToken);
    public async Task<IReadOnlyList<Guid>> GetAvailableCouriersAsync(Guid zoneId, CancellationToken cancellationToken) =>
        await (from profile in db.CourierAvailabilities
               join userRole in db.UserRoles on profile.CourierUserId equals userRole.UserId
               join role in db.Roles on userRole.RoleId equals role.Id
               where profile.IsAvailable && profile.ZoneId == zoneId && role.Name == "ExternalCourier"
               select profile.CourierUserId).Distinct().ToArrayAsync(cancellationToken);
    public async Task<IReadOnlySet<Guid>> GetCouriersWithActiveOffersAsync(Guid orderId, DateTimeOffset now, CancellationToken cancellationToken) =>
        (await db.ExternalDeliveryOffers.Where(x => x.OrderId == orderId && x.Status == ExternalDeliveryOfferStatus.Offered)
            .Select(x => new { x.CourierUserId, x.ExpiresAtUtc }).ToArrayAsync(cancellationToken))
            .Where(x => x.ExpiresAtUtc > now).Select(x => x.CourierUserId).ToHashSet();
    public Task<CourierAvailability?> GetAvailabilityAsync(Guid courierId, CancellationToken cancellationToken) =>
        db.CourierAvailabilities.SingleOrDefaultAsync(x => x.CourierUserId == courierId, cancellationToken);
    public Task<bool> IsActiveZoneAsync(Guid zoneId, CancellationToken cancellationToken) =>
        db.DeliveryZones.AnyAsync(x => x.Id == zoneId && x.IsActive, cancellationToken);
    public async Task<IReadOnlyList<ExternalDeliveryOffer>> GetOffersAsync(Guid courierId, CancellationToken cancellationToken) =>
        await db.ExternalDeliveryOffers.Where(x => x.CourierUserId == courierId && x.Status == ExternalDeliveryOfferStatus.Offered)
            .ToListAsync(cancellationToken);
    public async Task<IReadOnlyList<ExternalDeliveryOffer>> GetOffersForOrderAsync(Guid orderId, CancellationToken cancellationToken) =>
        await db.ExternalDeliveryOffers.Where(x => x.OrderId == orderId && x.Status == ExternalDeliveryOfferStatus.Offered)
            .ToListAsync(cancellationToken);
    public Task<ExternalDeliveryOffer?> GetOfferAsync(Guid offerId, CancellationToken cancellationToken) =>
        db.ExternalDeliveryOffers.SingleOrDefaultAsync(x => x.Id == offerId, cancellationToken);
    public Task<bool> HasAcceptedOfferAsync(Guid orderId, CancellationToken cancellationToken) =>
        db.ExternalDeliveryOffers.AnyAsync(x => x.OrderId == orderId && x.Status == ExternalDeliveryOfferStatus.Accepted, cancellationToken);
    public async Task<IReadOnlyList<ExternalDeliveryOffer>> ExpireDueOffersAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        ExternalDeliveryOffer[] dueOffers;
        if (db.Database.IsSqlServer())
        {
            dueOffers = await db.ExternalDeliveryOffers.Where(x => x.Status == ExternalDeliveryOfferStatus.Offered &&
                x.ExpiresAtUtc <= now).ToArrayAsync(cancellationToken);
        }
        else
        {
            var openOffers = await db.ExternalDeliveryOffers.Where(x => x.Status == ExternalDeliveryOfferStatus.Offered)
                .ToListAsync(cancellationToken);
            dueOffers = openOffers.Where(x => x.ExpiresAtUtc <= now).ToArray();
        }
        foreach (var offer in dueOffers) offer.Expire(now);
        if (dueOffers.Length > 0) await db.SaveChangesAsync(cancellationToken);
        return dueOffers;
    }
    public void AddOffer(ExternalDeliveryOffer offer) => db.ExternalDeliveryOffers.Add(offer);
    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        foreach (var entry in db.ChangeTracker.Entries<OrderStatusChange>().Where(entry => entry.State == EntityState.Modified))
            entry.State = EntityState.Added;
        await db.SaveChangesAsync(cancellationToken);
    }
}
