namespace MoodPickup.Api.Entities;

public sealed class OrderItem
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    // This is a historical identifier, intentionally not a foreign key to Products.
    public Guid ProductId { get; set; }

    public string ProductName { get; set; } = string.Empty;

    // Historical product availability at the instant this item was purchased.
    public bool IsAvailableAtPurchase { get; set; }

    public decimal BasePrice { get; set; }

    public decimal FinalPrice { get; set; }

    public int? Calories { get; set; }

    public int? VolumeMilliliters { get; set; }

    public int? WeightGrams { get; set; }

    public int Quantity { get; set; }

    public string? Comment { get; set; }

    public ICollection<OrderItemOption> Options { get; set; } = [];
}
