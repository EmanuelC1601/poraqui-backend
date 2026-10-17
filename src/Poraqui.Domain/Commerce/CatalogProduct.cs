namespace Poraqui.Domain.Commerce;

public sealed class CatalogProduct
{
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

    public void Rename(string name) => Name = string.IsNullOrWhiteSpace(name)
        ? throw new ArgumentException("A product name is required.", nameof(name))
        : name.Trim();

    public void ChangePrice(decimal price) => Price = price < 0
        ? throw new ArgumentOutOfRangeException(nameof(price), "Price cannot be negative.")
        : decimal.Round(price, 2, MidpointRounding.AwayFromZero);

    public void SetAvailable(bool available) => IsAvailable = available;
}
