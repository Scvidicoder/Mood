using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;

namespace MoodPickup.Api.Interfaces;

public interface IAdminOptionService
{
    Task<PagedResponse<AdminOptionGroupDto>> GetGroupsAsync(
        AdminOptionGroupQuery query,
        CancellationToken cancellationToken);

    Task<AdminOptionGroupDto> GetGroupAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<AdminOptionGroupDto> CreateGroupAsync(
        CreateOptionGroupRequest request,
        CancellationToken cancellationToken);

    Task<AdminOptionGroupDto> UpdateGroupAsync(
        Guid id,
        UpdateOptionGroupRequest request,
        CancellationToken cancellationToken);

    Task<AdminOptionGroupDto> SetGroupActiveAsync(
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken);

    Task DeleteGroupAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken);

    Task<AdminOptionGroupDto> RestoreGroupAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminOptionValueDto>> GetValuesAsync(
        Guid groupId,
        bool includeDeleted,
        CancellationToken cancellationToken);

    Task<AdminOptionValueDto> CreateValueAsync(
        Guid groupId,
        CreateOptionValueRequest request,
        CancellationToken cancellationToken);

    Task<AdminOptionValueDto> UpdateValueAsync(
        Guid id,
        UpdateOptionValueRequest request,
        CancellationToken cancellationToken);

    Task<AdminOptionValueDto> SetValueActiveAsync(
        Guid id,
        SetActiveRequest request,
        CancellationToken cancellationToken);

    Task DeleteValueAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken);

    Task<AdminOptionValueDto> RestoreValueAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken);
}
