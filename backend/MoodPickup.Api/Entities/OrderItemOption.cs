namespace MoodPickup.Api.Entities;

public sealed class OrderItemOption
{
    public Guid Id { get; set; }

    public Guid OrderItemId { get; set; }

    public OrderItem OrderItem { get; set; } = null!;

    // Historical identifiers only; deliberately not foreign keys to mutable menu data.
    public Guid? OptionGroupId { get; set; }

    public Guid? OptionValueId { get; set; }

    public string OptionGroupName { get; set; } = string.Empty;

    public string OptionValueName { get; set; } = string.Empty;

    public decimal PriceModifier { get; set; }

    public int? CaloriesModifier { get; set; }

    public int? VolumeModifier { get; set; }

    public int DisplayOrder { get; set; }
}
