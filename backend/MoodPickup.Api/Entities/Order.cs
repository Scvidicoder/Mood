namespace MoodPickup.Api.Entities;

public sealed class Order : IHasCreatedAt, IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public Guid CustomerId { get; set; }

    public Customer Customer { get; set; } = null!;

    public string OrderNumber { get; set; } = string.Empty;

    public OrderStatus Status { get; set; } = OrderStatus.PendingConfirmation;

    public PaymentMethod PaymentMethod { get; set; }

    public PickupMode PickupMode { get; set; }

    public DateTimeOffset? RequestedPickupTime { get; set; }

    public string CustomerName { get; set; } = string.Empty;

    public string CustomerPhoneNumber { get; set; } = string.Empty;

    public string? Comment { get; set; }

    public decimal Subtotal { get; set; }

    public decimal DiscountTotal { get; set; }

    public decimal Total { get; set; }

    public string Currency { get; set; } = string.Empty;

    public DateTimeOffset? EstimatedReadyAt { get; set; }

    public Guid? ConfirmedByEmployeeId { get; set; }

    public Employee? ConfirmedByEmployee { get; set; }

    public DateTimeOffset? ConfirmedAt { get; set; }

    public Guid? RejectedByEmployeeId { get; set; }

    public Employee? RejectedByEmployee { get; set; }

    public DateTimeOffset? RejectedAt { get; set; }

    public string? RejectReason { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];
}
