namespace MoodPickup.Api.Entities;

public sealed class OptionValue : IHasTimestamps, IHasConcurrencyToken, IHasNormalizedName
{
    public Guid Id { get; set; }

    public Guid OptionGroupId { get; set; }

    public OptionGroup OptionGroup { get; set; } = null!;

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public ICollection<ProductOptionValue> ProductAssignments { get; set; } = [];
}
