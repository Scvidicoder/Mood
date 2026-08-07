using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;

namespace MoodPickup.Api.Interfaces;

public interface IAdminProductConfigurationService
{
    Task<MenuMutationResponse<AdminProductOptionGroupDto>> AddGroupAsync(
        Guid productId,
        CreateProductOptionGroupRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductOptionGroupDto>> UpdateGroupAsync(
        Guid productId,
        Guid assignmentId,
        UpdateProductOptionGroupRequest request,
        CancellationToken cancellationToken);

    Task DeleteGroupAsync(
        Guid productId,
        Guid assignmentId,
        Guid rowVersion,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductOptionGroupDto>> RestoreGroupAsync(
        Guid productId,
        Guid assignmentId,
        RowVersionRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductOptionValueDto>> AddValueAsync(
        Guid productId,
        Guid assignmentId,
        CreateProductOptionValueRequest request,
        CancellationToken cancellationToken);

    Task<MenuMutationResponse<AdminProductOptionValueDto>> UpdateValueAsync(
        Guid productId,
        Guid assignmentValueId,
        UpdateProductOptionValueRequest request,
        CancellationToken cancellationToken);

    Task DeleteValueAsync(
        Guid productId,
        Guid assignmentValueId,
        Guid rowVersion,
        CancellationToken cancellationToken);
}
