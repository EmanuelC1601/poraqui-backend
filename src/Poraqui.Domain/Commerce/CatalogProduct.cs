namespace Poraqui.Domain.Commerce;

public sealed class CatalogProduct
{
    private readonly List<ProductOptionGroup> _optionGroups = [];
    private CatalogProduct() { }

    public CatalogProduct(Guid businessId, string name, decimal price)
    {
        if (businessId == Guid.Empty) throw new ArgumentException("A business is required.", nameof(businessId));
        Id = Guid.NewGuid();
        BusinessId = businessId;
        Rename(name);
        ChangePrice(price);
    }

    public Guid Id { get; private set; }
    public Guid BusinessId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal Price { get; private set; }
    public bool IsAvailable { get; private set; } = true;
    public IReadOnlyCollection<ProductOptionGroup> OptionGroups => _optionGroups.AsReadOnly();

    public ProductOptionGroup AddOptionGroup(string name, int minSelections, int maxSelections,
        IEnumerable<(string Name, decimal PriceDelta)> options)
    {
        if (_optionGroups.Any(group => string.Equals(group.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase)))
            throw new ArgumentException("An option group with this name already exists.", nameof(name));
        var group = new ProductOptionGroup(Id, name, minSelections, maxSelections, options);
        _optionGroups.Add(group);
        return group;
    }

    public void Rename(string name) => Name = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("A product name is required.", nameof(name))
        : name.Trim();

    public void ChangePrice(decimal price) => Price = price < 0
        ? throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.")
        : decimal.Round(price, 2, MidpointRounding.AwayFromZero);

    public void SetAvailable(bool available) => IsAvailable = available;
}
