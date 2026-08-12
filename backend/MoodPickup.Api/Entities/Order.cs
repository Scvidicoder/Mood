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

    public DateTimeOffset? PreparationStartedAt { get; set; }

    public Guid? PreparationStartedByEmployeeId { get; set; }

    public Employee? PreparationStartedByEmployee { get; set; }

    public DateTimeOffset? ReadyAt { get; set; }

    public Guid? ReadyByEmployeeId { get; set; }

    public Employee? ReadyByEmployee { get; set; }

    public bool PaymentReceived { get; set; }

    public PaymentMethodUsed? PaymentMethodUsed { get; set; }

    public DateTimeOffset? PaymentReceivedAt { get; set; }

    public Guid? PaymentReceivedByEmployeeId { get; set; }

    public Employee? PaymentReceivedByEmployee { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public Guid? CompletedByEmployeeId { get; set; }

    public Employee? CompletedByEmployee { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public Payment? Payment { get; set; }

    public ICollection<OrderItem> Items { get; set; } = [];

    public ICollection<OrderStatusHistory> StatusHistory { get; set; } = [];
}
