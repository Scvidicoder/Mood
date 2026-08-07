namespace MoodPickup.Api.Entities;

public sealed class OptionGroup : IHasTimestamps, IHasConcurrencyToken, IHasNormalizedName
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string NormalizedName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public OptionSelectionType SelectionType { get; set; }

    public bool DefaultIsRequired { get; set; }

    public int DefaultMinimumSelections { get; set; }

    public int? DefaultMaximumSelections { get; set; }

    public int DisplayOrder { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public ICollection<OptionValue> Values { get; set; } = [];

    public ICollection<ProductOptionGroup> ProductAssignments { get; set; } = [];
}
