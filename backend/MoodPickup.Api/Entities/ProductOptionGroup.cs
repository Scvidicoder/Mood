namespace MoodPickup.Api.Entities;

public sealed class ProductOptionGroup : IHasTimestamps, IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public Guid ProductId { get; set; }

    public Product Product { get; set; } = null!;

    public Guid OptionGroupId { get; set; }

    public OptionGroup OptionGroup { get; set; } = null!;

    public bool IsRequired { get; set; }

    public int MinimumSelections { get; set; }

    public int MaximumSelections { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public ICollection<ProductOptionValue> Values { get; set; } = [];
}
