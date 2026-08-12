namespace MoodPickup.Api.Entities;

public sealed class EmployeeActionLog : IHasCreatedAt
{
    public Guid Id { get; set; }

    public Guid? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public string ActionType { get; set; } = string.Empty;

    public string EntityType { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    public string Description { get; set; } = string.Empty;

    public string? OldValuesJson { get; set; }

    public string? NewValuesJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public string CorrelationId { get; set; } = string.Empty;
}
