using MoodPickup.Api.DTOs.Menu;

namespace MoodPickup.Api.DTOs.Employees;

public enum EmployeeStatusFilter
{
    All,
    Active,
    Disabled
}

public sealed class EmployeeListQuery : PaginationQuery
{
    public string? Search { get; init; }

    public string? Role { get; init; }

    public EmployeeStatusFilter Status { get; init; } = EmployeeStatusFilter.All;
}

public sealed record EmployeeListItemDto(
    Guid Id,
    string FullName,
    string Username,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLoginAt,
    Guid RowVersion);

public sealed record EmployeeDetailsDto(
    Guid Id,
    string FullName,
    string Username,
    IReadOnlyList<string> Roles,
    bool IsActive,
    bool MustChangePassword,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? LastLoginAt,
    Guid RowVersion);

public sealed record CreateEmployeeRequest(
    string FullName,
    string Username,
    IReadOnlyList<string> Roles);

public sealed record CreateEmployeeResponse(
    EmployeeDetailsDto Employee,
    string TemporaryPassword);

public sealed record UpdateEmployeeRequest(
    string FullName,
    string Username,
    IReadOnlyList<string> Roles,
    Guid RowVersion);

public sealed record EmployeeVersionRequest(Guid RowVersion);

public sealed record ResetEmployeePasswordResponse(
    string TemporaryPassword,
    bool MustChangePassword,
    Guid RowVersion,
    int RevokedSessionCount);

public sealed class EmployeeActionQuery : PaginationQuery
{
    public string? ActionType { get; init; }

    public string? EntityType { get; init; }

    public DateTimeOffset? DateFrom { get; init; }

    public DateTimeOffset? DateTo { get; init; }
}

public sealed record EmployeeActionListItemDto(
    Guid Id,
    DateTimeOffset Timestamp,
    Guid? ActingEmployeeId,
    string ActingEmployeeName,
    string ActionType,
    string EntityType,
    Guid EntityId,
    string Description,
    string CorrelationId);

public sealed record RoleOptionDto(string Name, string DisplayName);

public sealed record EmployeePermissionDto(
    string Permission,
    string DisplayName,
    string Group,
    bool RoleAllowed,
    bool? Override,
    bool IsAllowed);

public sealed record EmployeePermissionsResponse(
    Guid EmployeeId,
    IReadOnlyList<EmployeePermissionDto> Permissions);

public sealed record EmployeePermissionOverrideRequest(
    string Permission,
    bool IsAllowed);

public sealed record ReplaceEmployeePermissionsRequest(
    IReadOnlyList<EmployeePermissionOverrideRequest> Overrides);
