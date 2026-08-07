using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;

namespace MoodPickup.Api.Interfaces;

public interface IAdminCategoryService
{
    Task<PagedResponse<AdminCategoryDto>> GetCategoriesAsync(
        AdminCategoryQuery query,
        CancellationToken cancellationToken);

    Task<AdminCategoryDto> GetCategoryAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<AdminCategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<AdminCategoryDto> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminCategoryDto>> ReorderAsync(
        ReorderCategoriesRequest request,
        CancellationToken cancellationToken);

    Task<AdminCategoryDto> SetVisibilityAsync(
        Guid id,
        SetVisibilityRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken);

    Task<AdminCategoryDto> RestoreAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken);
}
