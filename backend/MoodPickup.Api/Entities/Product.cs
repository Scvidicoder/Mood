namespace MoodPickup.Api.Entities;

public sealed class Product : IHasTimestamps, IHasConcurrencyToken, IHasNormalizedName
{
    public Guid Id { get; set; }

    public Guid CategoryId { get; set; }

    public Category Category { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? ShortDescription { get; set; }

    public string? Description { get; set; }

    public string? Ingredients { get; set; }

    public decimal BasePrice { get; set; }

    public int? DefaultWeightGrams { get; set; }

    public int? DefaultVolumeMilliliters { get; set; }

    public int? DefaultCalories { get; set; }

    public Guid? ImageId { get; set; }

    public MediaFile? Image { get; set; }

    public bool IsAvailable { get; set; } = true;

    public bool IsVisible { get; set; } = true;

    public bool IsDeleted { get; set; }

    public int DisplayOrder { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public ICollection<ProductOptionGroup> OptionGroups { get; set; } = [];
}
