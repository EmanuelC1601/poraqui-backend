using Microsoft.EntityFrameworkCore;
using Poraqui.Application.Commerce;
using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;

namespace Poraqui.Infrastructure.Persistence;

public sealed class CatalogOrderRepository(AppDbContext db) : ICatalogOrderRepository
{
    public Task<Business?> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken) =>
        db.Businesses.SingleOrDefaultAsync(item => item.Id == businessId, cancellationToken);

    public Task<Business?> GetBusinessOwnedByAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken) =>
        db.Businesses.SingleOrDefaultAsync(item => item.Id == businessId && item.OwnerUserId == ownerId, cancellationToken);

    public Task<CatalogProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken) =>
        db.CatalogProducts.Include(item => item.OptionGroups).ThenInclude(group => group.Options)
            .SingleOrDefaultAsync(item => item.Id == productId, cancellationToken);

    public Task<ProductOptionGroup?> GetOptionGroupAsync(Guid optionGroupId, CancellationToken cancellationToken) =>
        db.Set<ProductOptionGroup>().Include(group => group.Options)
            .SingleOrDefaultAsync(group => group.Id == optionGroupId, cancellationToken);

    public Task<ProductOption?> GetOptionAsync(Guid optionId, CancellationToken cancellationToken) =>
        db.Set<ProductOption>().SingleOrDefaultAsync(option => option.Id == optionId, cancellationToken);

    public async Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(Guid businessId, CancellationToken cancellationToken) =>
        await db.CatalogProducts.AsNoTracking().Include(item => item.OptionGroups).ThenInclude(group => group.Options)
            .Where(item => item.BusinessId == businessId).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken) =>
        await db.CatalogProducts.Include(item => item.OptionGroups).ThenInclude(group => group.Options)
            .Where(item => productIds.Contains(item.Id)).ToListAsync(cancellationToken);

    public Task<DeliveryZone?> GetZoneAsync(Guid zoneId, CancellationToken cancellationToken) =>
        db.DeliveryZones.SingleOrDefaultAsync(item => item.Id == zoneId, cancellationToken);

    public async Task<IReadOnlyList<DeliveryZone>> GetActiveZonesAsync(CancellationToken cancellationToken) =>
        await db.DeliveryZones.AsNoTracking().Where(item => item.IsActive).OrderBy(item => item.Name).ToListAsync(cancellationToken);

    public Task<DeliveryRate?> GetRateAsync(Guid businessId, Guid zoneId, CancellationToken cancellationToken) =>
        db.DeliveryRates.SingleOrDefaultAsync(item => item.BusinessId == businessId && item.ZoneId == zoneId, cancellationToken);

    public async Task<IReadOnlyList<DeliveryRate>> GetRatesAsync(Guid businessId, CancellationToken cancellationToken) =>
        await db.DeliveryRates.AsNoTracking().Where(item => item.BusinessId == businessId).OrderBy(item => item.ZoneId).ToListAsync(cancellationToken);

    public async Task AddProductAsync(CatalogProduct product, CancellationToken cancellationToken) => await db.CatalogProducts.AddAsync(product, cancellationToken);
    public async Task AddOptionGroupAsync(ProductOptionGroup group, CancellationToken cancellationToken) => await db.Set<ProductOptionGroup>().AddAsync(group, cancellationToken);
    public async Task AddZoneAsync(DeliveryZone zone, CancellationToken cancellationToken) => await db.DeliveryZones.AddAsync(zone, cancellationToken);
    public async Task AddRateAsync(DeliveryRate rate, CancellationToken cancellationToken) => await db.DeliveryRates.AddAsync(rate, cancellationToken);
    public async Task AddOrderAsync(Order order, CancellationToken cancellationToken) => await db.Orders.AddAsync(order, cancellationToken);
    public async Task SaveChangesAsync(CancellationToken cancellationToken) => await db.SaveChangesAsync(cancellationToken);
}
