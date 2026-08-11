using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Interfaces;

public interface IEmployeeManagementService
{
    Task<PagedResponse<EmployeeListItemDto>> GetAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken);

    Task<EmployeeDetailsDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<RoleOptionDto>> GetRolesAsync(
        CancellationToken cancellationToken);

    Task<EmployeePermissionsResponse> GetPermissionsAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<EmployeePermissionsResponse> ReplacePermissionOverridesAsync(
        Guid id,
        ReplaceEmployeePermissionsRequest request,
        CancellationToken cancellationToken);

    Task<CreateEmployeeResponse> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeDetailsDto> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken);

    Task<EmployeeDetailsDto> DisableAsync(
        Guid id,
        EmployeeVersionRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<EmployeeDetailsDto> EnableAsync(
        Guid id,
        EmployeeVersionRequest request,
        CancellationToken cancellationToken);

    Task<ResetEmployeePasswordResponse> ResetPasswordAsync(
        Guid id,
        EmployeeVersionRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken);

    Task<PagedResponse<EmployeeActionListItemDto>> GetActionsAsync(
        Guid id,
        EmployeeActionQuery query,
        CancellationToken cancellationToken);
}
