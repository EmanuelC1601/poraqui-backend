using Poraqui.Domain.Commerce;
using Poraqui.Domain.Orders;

namespace Poraqui.Application.Commerce;

public interface ICatalogOrderRepository
{
    Task<OrderCreationCommand?> GetOrderCreationCommandAsync(Guid customerId, Guid commandId, CancellationToken cancellationToken);
    Task<Order?> GetOrderForCreationReplayAsync(Guid orderId, CancellationToken cancellationToken);
    Task<Business?> GetBusinessAsync(Guid businessId, CancellationToken cancellationToken);
    Task<Business?> GetBusinessOwnedByAsync(Guid businessId, Guid ownerId, CancellationToken cancellationToken);
    Task<CatalogProduct?> GetProductAsync(Guid productId, CancellationToken cancellationToken);
    Task<ProductOptionGroup?> GetOptionGroupAsync(Guid optionGroupId, CancellationToken cancellationToken);
    Task<ProductOption?> GetOptionAsync(Guid optionId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(Guid businessId, CancellationToken cancellationToken);
    Task<IReadOnlyList<CatalogProduct>> GetProductsAsync(IReadOnlyCollection<Guid> productIds, CancellationToken cancellationToken);
    Task<DeliveryZone?> GetZoneAsync(Guid zoneId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryZone>> GetActiveZonesAsync(CancellationToken cancellationToken);
    Task<DeliveryRate?> GetRateAsync(Guid businessId, Guid zoneId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DeliveryRate>> GetRatesAsync(Guid businessId, CancellationToken cancellationToken);
    Task AddProductAsync(CatalogProduct product, CancellationToken cancellationToken);
    Task AddOptionGroupAsync(ProductOptionGroup group, CancellationToken cancellationToken);
    Task AddZoneAsync(DeliveryZone zone, CancellationToken cancellationToken);
    Task AddRateAsync(DeliveryRate rate, CancellationToken cancellationToken);
    Task AddOrderAsync(Order order, CancellationToken cancellationToken);
    Task<bool> SaveOrderCreationAsync(Order order, OrderCreationCommand command, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IOrderCommandSecretProtector
{
    string Protect(string secret);
    string Unprotect(string protectedSecret);
}

public sealed record ProductOptionView(Guid Id, string Name, decimal PriceDelta, bool IsAvailable);
public sealed record ProductOptionGroupView(Guid Id, string Name, int MinSelections, int MaxSelections, bool IsAvailable,
    IReadOnlyList<ProductOptionView> Options);
public sealed record ProductView(Guid Id, Guid BusinessId, string Name, decimal Price, bool IsAvailable,
    IReadOnlyList<ProductOptionGroupView> OptionGroups);
public sealed record OptionRequest(string Name, decimal PriceDelta);
public sealed record OptionGroupRequest(string Name, int MinSelections, int MaxSelections, IReadOnlyCollection<OptionRequest> Options);
public sealed record AvailabilityRequest(bool IsAvailable);
public sealed record ZoneView(Guid Id, string Name, bool IsActive);
public sealed record RateView(Guid Id, Guid BusinessId, Guid ZoneId, decimal Fee, bool IsActive);
public sealed record OpeningHoursRequest(DayOfWeek Day, bool IsClosed, TimeOnly? OpensAt, TimeOnly? ClosesAt);
public sealed record OpeningHoursView(string TimeZoneId, bool IsConfigured, bool IsAcceptingOrders,
    IReadOnlyList<OpeningHoursRequest> Days);
public sealed record OrderItemRequest(Guid ProductId, int Quantity, IReadOnlyCollection<Guid>? OptionIds = null);
public sealed record CreatedOrderView(Guid Id, string OrderNumber, Guid BusinessId, Guid ZoneId,
    string DeliveryAddress, decimal Subtotal, decimal DeliveryFee, decimal Total, OrderStatus Status,
    IReadOnlyList<CreatedOrderLineView> Items, string CustomerDeliveryCode, TransferInstructionsView TransferInstructions);
public sealed record SelectedOrderOptionView(Guid OptionId, string Name, decimal PriceDelta);
public sealed record CreatedOrderLineView(Guid ProductId, string ProductName, decimal UnitPrice, int Quantity, decimal LineTotal,
    IReadOnlyList<SelectedOrderOptionView> Options);
public sealed record TransferInstructionsView(string RecipientName, string Institution, string AccountReference, string? Instructions);

public sealed class CatalogOrderService(ICatalogOrderRepository repository, Poraqui.Application.Orders.IOrderChangePublisher publisher,
    IOrderCommandSecretProtector secretProtector)
{
    public async Task<IReadOnlyList<ProductView>> GetProductsAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var business = await repository.GetBusinessAsync(businessId, cancellationToken);
        if (business is null || !business.IsActive) return [];
        return (await repository.GetProductsAsync(businessId, cancellationToken))
            .Where(product => product.IsAvailable)
            .Select(ToView).ToArray();
    }

    public async Task<OpeningHoursView?> GetOpeningHoursAsync(Guid businessId, CancellationToken cancellationToken)
    {
        var business = await repository.GetBusinessAsync(businessId, cancellationToken);
        return business is null ? null : ToOpeningHoursView(business);
    }

    public async Task<OpeningHoursView?> ConfigureOpeningHoursAsync(Guid businessId, Guid actorId, bool isAdmin,
        string timeZoneId, IReadOnlyCollection<OpeningHoursRequest> days, CancellationToken cancellationToken)
    {
        var business = await repository.GetBusinessAsync(businessId, cancellationToken);
        if (business is null) return null;
        if (!isAdmin && await repository.GetBusinessOwnedByAsync(businessId, actorId, cancellationToken) is null)
            throw new BusinessAuthorizationException();
        business.ConfigureOpeningHours(timeZoneId,
            days.Select(day => (day.Day, day.IsClosed, day.OpensAt, day.ClosesAt)));
        await repository.SaveChangesAsync(cancellationToken);
        return ToOpeningHoursView(business);
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

    public async Task<ProductOptionGroupView?> AddOptionGroupAsync(Guid productId, Guid ownerId, bool isAdmin,
        OptionGroupRequest request, CancellationToken cancellationToken)
    {
        if (request.Options is null) throw new ArgumentException("Options are required.", nameof(request));
        var product = await repository.GetProductAsync(productId, cancellationToken);
        if (product is null) return null;
        var business = isAdmin
            ? await repository.GetBusinessAsync(product.BusinessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(product.BusinessId, ownerId, cancellationToken);
        if (business is null) throw new BusinessAuthorizationException();
        var group = product.AddOptionGroup(request.Name, request.MinSelections, request.MaxSelections,
            request.Options.Select(option => (option.Name, option.PriceDelta)));
        await repository.AddOptionGroupAsync(group, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(group);
    }

    public async Task<ProductOptionGroupView?> SetOptionGroupAvailabilityAsync(Guid optionGroupId, Guid ownerId,
        bool isAdmin, bool isAvailable, CancellationToken cancellationToken)
    {
        var group = await repository.GetOptionGroupAsync(optionGroupId, cancellationToken);
        if (group is null) return null;
        var product = await repository.GetProductAsync(group.ProductId, cancellationToken);
        var business = product is null ? null : isAdmin
            ? await repository.GetBusinessAsync(product.BusinessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(product.BusinessId, ownerId, cancellationToken);
        if (business is null) throw new BusinessAuthorizationException();
        group.SetAvailable(isAvailable);
        await repository.SaveChangesAsync(cancellationToken);
        return ToView(group);
    }

    public async Task<ProductOptionView?> SetOptionAvailabilityAsync(Guid optionId, Guid ownerId, bool isAdmin,
        bool isAvailable, CancellationToken cancellationToken)
    {
        var option = await repository.GetOptionAsync(optionId, cancellationToken);
        if (option is null) return null;
        var group = await repository.GetOptionGroupAsync(option.GroupId, cancellationToken);
        var product = group is null ? null : await repository.GetProductAsync(group.ProductId, cancellationToken);
        var business = product is null ? null : isAdmin
            ? await repository.GetBusinessAsync(product.BusinessId, cancellationToken)
            : await repository.GetBusinessOwnedByAsync(product.BusinessId, ownerId, cancellationToken);
        if (business is null) throw new BusinessAuthorizationException();
        option.SetAvailable(isAvailable);
        await repository.SaveChangesAsync(cancellationToken);
        return new ProductOptionView(option.Id, option.Name, option.PriceDelta, option.IsAvailable);
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
        Guid commandId, string deliveryAddress, IReadOnlyCollection<OrderItemRequest> requestedItems, CancellationToken cancellationToken)
    {
        if (commandId == Guid.Empty) throw new ArgumentException("A command ID is required.", nameof(commandId));
        if (requestedItems.Count == 0) throw new ArgumentException("At least one product is required.", nameof(requestedItems));
        if (requestedItems.Any(item => item.Quantity <= 0)) throw new ArgumentException("Quantities must be positive.", nameof(requestedItems));
        if (requestedItems.Select(item => item.ProductId).Distinct().Count() != requestedItems.Count)
            throw new ArgumentException("Each product may appear only once; combine its quantity.", nameof(requestedItems));

        var requestHash = HashOrderRequest(businessId, zoneId, deliveryAddress, requestedItems);
        var existing = await repository.GetOrderCreationCommandAsync(customerId, commandId, cancellationToken);
        if (existing is not null) return await ReplayOrderCreation(existing, requestHash, cancellationToken);

        var business = await repository.GetBusinessAsync(businessId, cancellationToken);
        if (business is null || !business.IsActive) throw new ArgumentException("Business is unavailable.", nameof(businessId));
        if (!business.IsAcceptingOrdersAt(DateTimeOffset.UtcNow))
            throw new ArgumentException("Business is currently outside its configured opening hours.", nameof(businessId));
        if (!business.HasTransferDetails) throw new ArgumentException("Business has not configured transfer payment details.", nameof(businessId));
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
        var transferDetails = new OrderTransferDetails(business.TransferRecipientName!, business.TransferInstitution!,
            business.TransferAccountReference!, business.TransferInstructions);
        var order = new Order(customerId, businessId, zoneId, deliveryAddress, rate.Fee, lines, transferDetails);
        foreach (var item in requestedItems)
        {
            var product = byId[item.ProductId];
            var optionIds = item.OptionIds?.ToArray() ?? [];
            if (optionIds.Distinct().Count() != optionIds.Length)
                throw new ArgumentException("Each option may only be selected once.", nameof(requestedItems));
            var activeGroups = product.OptionGroups.Where(group => group.IsAvailable).ToArray();
            var availableOptions = activeGroups.SelectMany(group => group.Options.Where(option => option.IsAvailable)).ToArray();
            if (optionIds.Any(id => availableOptions.All(option => option.Id != id)))
                throw new ArgumentException("One or more selected options are unavailable.", nameof(requestedItems));
            foreach (var group in activeGroups)
            {
                var selectedCount = optionIds.Count(id => group.Options.Any(option => option.IsAvailable && option.Id == id));
                if (selectedCount < group.MinSelections || selectedCount > group.MaxSelections)
                    throw new ArgumentException($"Select between {group.MinSelections} and {group.MaxSelections} options for '{group.Name}'.", nameof(requestedItems));
            }
            order.ApplyLineOptions(product.Id, availableOptions.Where(option => optionIds.Contains(option.Id))
                .Select(option => (option.Id, option.Name, option.PriceDelta)));
        }
        var deliveryCode = OrderAccessCode.Generate();
        order.SetDeliveryCodeHash(OrderAccessCode.Hash(deliveryCode));
        var command = new OrderCreationCommand(customerId, commandId, order.Id, requestHash,
            secretProtector.Protect(deliveryCode), DateTimeOffset.UtcNow);
        var saved = await repository.SaveOrderCreationAsync(order, command, cancellationToken);
        if (!saved)
        {
            existing = await repository.GetOrderCreationCommandAsync(customerId, commandId, cancellationToken)
                ?? throw new InvalidOperationException("The order creation command could not be recovered after a concurrent retry.");
            return await ReplayOrderCreation(existing, requestHash, cancellationToken);
        }
        var created = order.StatusHistory.Single();
        await publisher.PublishAsync(new Poraqui.Application.Orders.OrderChangeNotice(order.Id, order.BusinessId,
            order.CustomerUserId, order.CourierUserId, order.Status.ToString(), order.Version, created.ChangedAtUtc), cancellationToken);
        return ToView(order, deliveryCode);
    }

    private async Task<CreatedOrderView> ReplayOrderCreation(OrderCreationCommand command, string requestHash,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(command.RequestHash, requestHash, StringComparison.Ordinal))
            throw new OrderCreationCommandConflictException();
        var order = await repository.GetOrderForCreationReplayAsync(command.OrderId, cancellationToken)
            ?? throw new InvalidOperationException("The order for a stored creation command was not found.");
        return ToView(order, secretProtector.Unprotect(command.ProtectedDeliveryCode));
    }

    private static string HashOrderRequest(Guid businessId, Guid zoneId, string deliveryAddress,
        IReadOnlyCollection<OrderItemRequest> items)
    {
        var canonical = System.Text.Json.JsonSerializer.Serialize(new
        {
            businessId,
            zoneId,
            deliveryAddress = deliveryAddress.Trim(),
            items = items.OrderBy(item => item.ProductId).Select(item => new
            {
                item.ProductId,
                item.Quantity,
                optionIds = (item.OptionIds ?? []).Order().ToArray()
            }).ToArray()
        });
        return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(canonical)));
    }

    private static ProductView ToView(CatalogProduct product) => new(product.Id, product.BusinessId, product.Name, product.Price,
        product.IsAvailable, product.OptionGroups.Where(group => group.IsAvailable).Select(group => new ProductOptionGroupView(group.Id,
            group.Name, group.MinSelections, group.MaxSelections, group.IsAvailable,
            group.Options.Where(option => option.IsAvailable).Select(option => new ProductOptionView(option.Id, option.Name,
                option.PriceDelta, option.IsAvailable)).ToArray())).ToArray());
    private static OpeningHoursView ToOpeningHoursView(Business business) => new(business.TimeZoneId,
        business.HasConfiguredOpeningHours, business.IsActive && business.IsAcceptingOrdersAt(DateTimeOffset.UtcNow),
        Enum.GetValues<DayOfWeek>().OrderBy(day => (int)day).Select(day =>
        {
            var entry = business.OpeningHours.SingleOrDefault(item => item.Day == day);
            return entry is null ? new OpeningHoursRequest(day, true, null, null)
                : new OpeningHoursRequest(day, entry.IsClosed, entry.OpensAt, entry.ClosesAt);
        }).ToArray());
    private static ProductOptionGroupView ToView(ProductOptionGroup group) => new(group.Id, group.Name, group.MinSelections,
        group.MaxSelections, group.IsAvailable, group.Options.Select(option => new ProductOptionView(option.Id, option.Name,
            option.PriceDelta, option.IsAvailable)).ToArray());
    private static CreatedOrderView ToView(Order order, string deliveryCode) => new(order.Id, order.OrderNumber, order.BusinessId, order.ZoneId,
        order.DeliveryAddress, order.Items.Sum(item => item.LineTotal), order.DeliveryFee, order.Total, order.Status,
        order.Items.Select(item => new CreatedOrderLineView(item.ProductId, item.ProductName, item.UnitPrice, item.Quantity, item.LineTotal,
            item.Options.Select(option => new SelectedOrderOptionView(option.ProductOptionId, option.Name, option.PriceDelta)).ToArray())).ToArray(), deliveryCode,
        new TransferInstructionsView(order.TransferRecipientName!, order.TransferInstitution!, order.TransferAccountReference!, order.TransferInstructions));
}

public sealed class OrderCreationCommandConflictException : Exception { }

public static class OrderAccessCode
{
    public static string Generate() => System.Security.Cryptography.RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
    public static string Hash(string value) => Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(value ?? string.Empty)));
}
