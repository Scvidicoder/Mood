using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Public;

namespace MoodPickup.Api.Interfaces;

public interface IPublicMenuService
{
    Task<IReadOnlyList<PublicCategoryDto>> GetCategoriesAsync(
        CancellationToken cancellationToken);

    Task<PagedResponse<PublicProductListItemDto>> GetProductsAsync(
        PublicProductQuery query,
        CancellationToken cancellationToken);

    Task<PublicProductDetailDto> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken);
}
