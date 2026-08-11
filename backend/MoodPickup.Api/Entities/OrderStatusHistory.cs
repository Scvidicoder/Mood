namespace MoodPickup.Api.Entities;

public sealed class OrderStatusHistory
{
    public Guid Id { get; set; }

    public Guid OrderId { get; set; }

    public Order Order { get; set; } = null!;

    public OrderStatus? OldStatus { get; set; }

    public OrderStatus NewStatus { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public Guid? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public string CorrelationId { get; set; } = string.Empty;

    public string? Reason { get; set; }
}
