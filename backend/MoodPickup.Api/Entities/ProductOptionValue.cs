namespace MoodPickup.Api.Entities;

public sealed class ProductOptionValue : IHasTimestamps, IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public Guid ProductOptionGroupId { get; set; }

    public ProductOptionGroup ProductOptionGroup { get; set; } = null!;

    public Guid OptionValueId { get; set; }

    public OptionValue OptionValue { get; set; } = null!;

    public decimal PriceModifier { get; set; }

    public bool IsDefault { get; set; }

    public bool IsAvailable { get; set; } = true;

    public int DisplayOrder { get; set; }

    public int? VolumeMilliliters { get; set; }

    public int? Calories { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid RowVersion { get; set; }
}
