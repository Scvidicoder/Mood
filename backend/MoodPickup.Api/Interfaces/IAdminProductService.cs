using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;

namespace MoodPickup.Api.Interfaces;

public interface IAdminProductService
{
    Task<PagedResponse<AdminProductListItemDto>> GetProductsAsync(
        AdminProductQuery query,
        CancellationToken cancellationToken);

    Task<AdminProductDto> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> DuplicateAsync(
        Guid id,
        DuplicateProductRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<AdminProductListItemDto>> ReorderAsync(
        ReorderProductsRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> SetAvailabilityAsync(
        Guid id,
        SetAvailabilityRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> SetVisibilityAsync(
        Guid id,
        SetVisibilityRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> AssignImageAsync(
        Guid id,
        AssignProductImageRequest request,
        CancellationToken cancellationToken);

    Task DeleteAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductDto>> RestoreAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken);
}
