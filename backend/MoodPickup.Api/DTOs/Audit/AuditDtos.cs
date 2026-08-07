using MoodPickup.Api.DTOs.Menu;

namespace MoodPickup.Api.DTOs.Audit;

public sealed class AuditLogQuery : PaginationQuery
{
    public Guid? EmployeeId { get; init; }

    public string? ActionType { get; init; }

    public string? EntityType { get; init; }

    public Guid? EntityId { get; init; }

    public DateTimeOffset? DateFrom { get; init; }

    public DateTimeOffset? DateTo { get; init; }
}

public sealed record AuditLogListItemDto(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid EmployeeId,
    string EmployeeName,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string Description,
    string CorrelationId);

public sealed record AuditLogDetailDto(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid EmployeeId,
    string EmployeeName,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string Description,
    string? OldValuesJson,
    string? NewValuesJson,
    string CorrelationId);
