using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;

namespace Poraqui.Application.Commerce;

public interface ICatalogOrderRepository
{
    Task<Business?> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken);
    Task<Business?> GetBusinessOwnedByAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken);
    Task<CatalogProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
    Task<DeliveryZone?> GetZoneAsync(Guid zoneId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryZone>> GetActiveZonesAsync(CancellationToken cancellationToken);
    Task<DeliveryRate?> GetRateAsync(Guid businessId, Guid zoneId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryRate>> GetRatesAsync(Guid businessId, CancellationToken cancellationToken);
    Task AddProductAsync(CatalogProduct product, CancellationToken cancellationToken);
    Task AddZoneAsync(DeliveryZone zone, CancellationToken cancellationToken);
    Task AddRateAsync(DeliveryRate rate, CancellationToken cancellationToken);
    Task AddOrderAsync(Order order, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public sealed record ProductView(Guid Id, Guid BusinessId, string Name, decimal Price, bool IsAvailable);
public sealed record ZoneView(Guid Id, string Name, bool IsActive);
public sealed record RateView(Guid Id, Guid BusinessId, Guid ZoneId, decimal Fee, bool IsActive);
public sealed record OrderItemRequest(Guid ProductId, int Quantity);
public sealed record CreatedOrderView(Guid Id, string OrderNumber, Guid BusinessId, Guid ZoneId,
    string DeliveryAddress, decimal Subtotal, decimal DeliveryFee, decimal Total, OrderStatus Status,
    IReadOnlyList<CreatedOrderLineView> Items);
public sealed record CreatedOrderLineView(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal);

public sealed class CatalogOrderService(ICatalogOrderRepository repository)
{
    public async Task<IReadOnlyList<ProductView>> GetProductsAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var business = await repository.GetBusinessAsync(businessId, cancellationToken);
        if (business is null || !business.IsActive) return [];
        return (await repository.GetProductsAsync(businessId, cancellationToken))
            .Where(product => product.IsAvailable)
            .Select(ToView).ToArray();
    }

    public async Task<ProductView?> AddProductAsync(Guid businessId, Guid ownerId, string name, decimal price,
        CancellationToken cancellationToken, bool isPlatformAdmin = false)
    {
        var business = isPlatformAdmin
            ? await repository.GetBusinessAsync(businessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(businessId, ownerId, cancellationToken);
        if (business is null)
            throw new BusinessAuthorizationException();
        var product = new CatalogProduct(businessId, name, price);
        await repository.AddProductAsync(product, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(product);
    }

    public async Task<ProductView?> UpdateProductAsync(Guid productId, Guid ownerId, string name, decimal price,
        bool available, CancellationToken cancellationToken, bool isPlatformAdmin = false)
    {
        var product = await repository.GetProductAsync(productId, cancellationToken);
        if (product is null) return null;
        var business = isPlatformAdmin
            ? await repository.GetBusinessAsync(product.BusinessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(product.BusinessId, ownerId, cancellationToken);
        if (business is null)
            throw new BusinessAuthorizationException();
        product.Rename(name);
        product.ChangePrice(price);
        product.SetAvailable(available);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(product);
    }

    public async Task<IReadOnlyList<ZoneView>> GetZonesAsync(CancellationToken cancellationToken) =>
        (await repository.GetActiveZonesAsync(cancellationToken)).Select(zone => new ZoneView(zone.Id, zone.Name, zone.IsActive)).ToArray();

    public async Task<ZoneView> AddZoneAsync(string name, CancellationToken cancellationToken)
    {
        var zone = new DeliveryZone(name);
        await repository.AddZoneAsync(zone, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new ZoneView(zone.Id, zone.Name, zone.IsActive);
    }

    public async Task<ZoneView?> UpdateZoneAsync(Guid zoneId, string name, bool active, CancellationToken cancellationToken)
    {
        var zone = await repository.GetZoneAsync(zoneId, cancellationToken);
        if (zone is null) return null;
        zone.Rename(name);
        zone.SetActive(active);
        await repository.SaveChangesAsync(cancellationToken);
        return new ZoneView(zone.Id, zone.Name, zone.IsActive);
    }

    public async Task<IReadOnlyList<RateView>> GetRatesAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken)
    {
        if (await repository.GetBusinessOwnedByAsync(businessId, ownerId, cancellationToken) is null)
            throw new BusinessAuthorizationException();
        return (await repository.GetRatesAsync(businessId, cancellationToken))
            .Select(rate => new RateView(rate.Id, rate.BusinessId, rate.ZoneId, rate.Fee, rate.IsActive)).ToArray();
    }

    public async Task<IReadOnlyList<RateView>> GetRatesForAdminAsync(Guid businessId, CancellationToken cancellationToken)
    {
        if (await repository.GetBusinessAsync(businessId, cancellationToken) is null) return [];
        return (await repository.GetRatesAsync(businessId, cancellationToken))
            .Select(rate => new RateView(rate.Id, rate.BusinessId, rate.ZoneId, rate.Fee, rate.IsActive)).ToArray();
    }

    public async Task<RateView?> AddRateAsync(Guid businessId, Guid ownerId, Guid zoneId, decimal fee,
        CancellationToken cancellationToken, bool isPlatformAdmin = false)
    {
        var business = isPlatformAdmin
            ? await repository.GetBusinessAsync(businessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(businessId, ownerId, cancellationToken);
        if (business is null)
            throw new BusinessAuthorizationException();
        var zone = await repository.GetZoneAsync(zoneId, cancellationToken);
        if (zone is null || !zone.IsActive) throw new ArgumentException("An active delivery zone is required.", nameof(zoneId));
        if (await repository.GetRateAsync(businessId, zoneId, cancellationToken) is not null)
            throw new ArgumentException("A delivery rate already exists for this business and zone.", nameof(zoneId));
        var rate = new DeliveryRate(businessId, zoneId, fee);
        await repository.AddRateAsync(rate, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return new RateView(rate.Id, businessId, zoneId, rate.Fee, rate.IsActive);
    }

    public async Task<RateView?> UpdateRateAsync(Guid businessId, Guid ownerId, Guid zoneId, decimal fee, bool active,
        CancellationToken cancellationToken, bool isPlatformAdmin = false)
    {
        var business = isPlatformAdmin
            ? await repository.GetBusinessAsync(businessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(businessId, ownerId, cancellationToken);
        if (business is null)
            throw new BusinessAuthorizationException();
        var rate = await repository.GetRateAsync(businessId, zoneId, cancellationToken);
        if (rate is null) return null;
        var zone = await repository.GetZoneAsync(zoneId, cancellationToken);
        if (active && (zone is null || !zone.IsActive))
            throw new ArgumentException("An active delivery zone is required.", nameof(zoneId));
        rate.ChangeFee(fee);
        rate.SetActive(active);
        await repository.SaveChangesAsync(cancellationToken);
        return new RateView(rate.Id, rate.BusinessId, rate.ZoneId, rate.Fee, rate.IsActive);
    }

    public async Task<CreatedOrderView> CreateOrderAsync(Guid customerId, Guid businessId, Guid zoneId,
        string deliveryAddress, IReadOnlyCollection<OrderItemRequest> requestedItems, CancellationToken cancellationToken)
    {
        if (requestedItems.Count == 0) throw new ArgumentException("At least one product is required.", nameof(requestedItems));
        if (requestedItems.Any(item => item.Quantity <= 0)) throw new ArgumentException("Quantities must be positive.", nameof(requestedItems));
        if (requestedItems.Select(item => item.ProductId).Distinct().Count() != requestedItems.Count)
            throw new ArgumentException("Each product may appear only once; combine its quantity.", nameof(requestedItems));

        var business = await repository.GetBusinessAsync(businessId, cancellationToken);
        if (business is null || !business.IsActive) throw new ArgumentException("Business is unavailable.", nameof(businessId));
        var zone = await repository.GetZoneAsync(zoneId, cancellationToken);
        if (zone is null || !zone.IsActive) throw new ArgumentException("Delivery zone is unavailable.", nameof(zoneId));
        var rate = await repository.GetRateAsync(businessId, zoneId, cancellationToken);
        if (rate is null || !rate.IsActive) throw new ArgumentException("Business does not deliver to this zone.", nameof(zoneId));

        var productIds = requestedItems.Select(item => item.ProductId).ToArray();
        var products = await repository.GetProductsAsync(productIds, cancellationToken);
        if (products.Count != requestedItems.Count || products.Any(product => product.BusinessId != businessId || !product.IsAvailable))
            throw new ArgumentException("One or more products are unavailable.", nameof(requestedItems));

        var byId = products.ToDictionary(product => product.Id);
        var lines = requestedItems.Select(item => (item.ProductId, byId[item.ProductId].Name, byId[item.ProductId].Price, item.Quantity)).ToArray();
        var order = new Order(customerId, businessId, zoneId, deliveryAddress, rate.Fee, lines);
        await repository.AddOrderAsync(order, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(order);
    }

    private static ProductView ToView(CatalogProduct product) => new(product.Id, product.BusinessId, product.Name, product.Price, product.IsAvailable);
    private static CreatedOrderView ToView(Order order) => new(order.Id, order.OrderNumber, order.BusinessId, order.ZoneId,
        order.DeliveryAddress, order.Items.Sum(item => item.LineTotal), order.DeliveryFee, order.Total, order.Status,
        order.Items.Select(item => new CreatedOrderLineView(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal)).ToArray());
}
