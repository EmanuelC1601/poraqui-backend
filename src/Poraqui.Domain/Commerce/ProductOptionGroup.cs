namespace Poraqui.Domain.Commerce;

public sealed class ProductOptionGroup
{
    private readonly List<ProductOption> _options = [];
    private ProductOptionGroup() { }

    public ProductOptionGroup(Guid productId, string name, int minSelections, int maxSelections,
        IEnumerable<(string Name, decimal PriceDelta)> options)
    {
        if (productId == Guid.Empty) throw new ArgumentException("A product is required.", nameof(productId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An option group name is required.", nameof(name));
        var choices = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));
        if (choices.Length == 0) throw new ArgumentException("At least one option is required.", nameof(options));
        if (choices.Select(choice => choice.Name?.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Count() != choices.Length)
            throw new ArgumentException("Option names in a group must be unique.", nameof(options));
        if (minSelections < 0 || maxSelections < 1 || minSelections > maxSelections || maxSelections > choices.Length)
            throw new ArgumentOutOfRangeException(nameof(maxSelections), "Selection limits must fit the option count.");
        Id = Guid.NewGuid();
        ProductId = productId;
        Name = name.Trim();
        MinSelections = minSelections;
        MaxSelections = maxSelections;
        foreach (var choice in choices) _options.Add(new ProductOption(Id, choice.Name, choice.PriceDelta));
    }

    public Guid Id { get; private set; }
    public Guid ProductId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public int MinSelections { get; private set; }
    public int MaxSelections { get; private set; }
    public bool IsAvailable { get; private set; } = true;
    public IReadOnlyCollection<ProductOption> Options => _options.AsReadOnly();
    public void SetAvailable(bool available) => IsAvailable = available;
}

public sealed class ProductOption
{
    private ProductOption() { }
    internal ProductOption(Guid groupId, string name, decimal priceDelta)
    {
        if (groupId == Guid.Empty) throw new ArgumentException("An option group is required.", nameof(groupId));
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("An option name is required.", nameof(name));
        if (priceDelta < 0) throw new ArgumentOutOfRangeException(nameof(priceDelta));
        Id = Guid.NewGuid();
        GroupId = groupId;
        Name = name.Trim();
        PriceDelta = decimal.Round(priceDelta, 2, MidpointRounding.AwayFromZero);
    }
    public Guid Id { get; private set; }
    public Guid GroupId { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public decimal PriceDelta { get; private set; }
    public bool IsAvailable { get; private set; } = true;
    public void SetAvailable(bool available) => IsAvailable = available;
}
