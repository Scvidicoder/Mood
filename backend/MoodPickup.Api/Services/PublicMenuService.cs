using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Public;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class PublicMenuService(
    MoodPickupDbContext dbContext,
    IMenuConfigurationValidator menuValidator,
    IMediaStorage mediaStorage) : IPublicMenuService
{
    public async Task<IReadOnlyList<PublicCategoryDto>> GetCategoriesAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .AsNoTracking()
            .Where(category =>
                category.IsVisible &&
                category.Products.Any(product =>
                    product.IsVisible &&
                    !product.IsDeleted))
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => new PublicCategoryDto(
                category.Id,
                category.Name,
                category.Description,
                category.DisplayOrder))
            .ToListAsync(cancellationToken);
    }

    public async Task<PagedResponse<PublicProductListItemDto>> GetProductsAsync(
        PublicProductQuery query,
        CancellationToken cancellationToken)
    {
        var products = dbContext.Products
            .AsNoTracking()
            .Where(product =>
                product.IsVisible &&
                product.Category.IsVisible &&
                !product.Category.IsDeleted);

        if (query.CategoryId is Guid categoryId)
        {
            products = products.Where(product => product.CategoryId == categoryId);
        }

        if (query.IncludeUnavailable == false)
        {
            products = products.Where(product => product.IsAvailable);
        }

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            products = products.Where(product =>
                product.NormalizedName.Contains(search) ||
                (product.ShortDescription != null &&
                 product.ShortDescription.ToLower().Contains(search)) ||
                (product.Description != null &&
                 product.Description.ToLower().Contains(search)));
        }

        var totalCount = await products.CountAsync(cancellationToken);
        var pageProducts = await products
            .OrderBy(product => product.Category.DisplayOrder)
            .ThenBy(product => product.DisplayOrder)
            .ThenBy(product => product.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(product => new ProductListProjection(
                product.Id,
                product.CategoryId,
                product.Name,
                product.ShortDescription,
                product.Image == null ? null : product.Image.StorageProvider,
                product.Image == null ? null : product.Image.StorageKey,
                product.BasePrice,
                product.DefaultWeightGrams,
                product.DefaultVolumeMilliliters,
                product.DefaultCalories,
                product.IsAvailable))
            .ToListAsync(cancellationToken);

        var productIds = pageProducts.Select(product => product.Id).ToArray();
        var groups = productIds.Length == 0
            ? new List<ProductGroupProjection>()
            : await dbContext.ProductOptionGroups
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(group =>
                    productIds.Contains(group.ProductId) &&
                    group.IsActive)
                .Select(group => new ProductGroupProjection(
                    group.Id,
                    group.ProductId,
                    group.OptionGroupId,
                    group.IsRequired,
                    group.OptionGroup.SelectionType,
                    group.OptionGroup.IsActive,
                    group.OptionGroup.IsDeleted))
                .ToListAsync(cancellationToken);

        var groupIds = groups.Select(group => group.Id).ToArray();
        var values = groupIds.Length == 0
            ? new List<ProductValueProjection>()
            : await dbContext.ProductOptionValues
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(value => groupIds.Contains(value.ProductOptionGroupId))
                .Select(value => new ProductValueProjection(
                    value.ProductOptionGroupId,
                    value.PriceModifier,
                    value.IsDefault,
                    value.IsAvailable,
                    value.OptionValue.OptionGroupId,
                    value.OptionValue.IsActive,
                    value.OptionValue.IsDeleted))
                .ToListAsync(cancellationToken);

        var items = pageProducts
            .Select(product => MapListItem(
                product,
                groups.Where(group => group.ProductId == product.Id),
                values))
            .ToArray();

        return new PagedResponse<PublicProductListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<PublicProductDetailDto> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await dbContext.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(item => item.Category)
            .Include(item => item.Image)
            .Include(item => item.OptionGroups)
                .ThenInclude(group => group.OptionGroup)
            .Include(item => item.OptionGroups)
                .ThenInclude(group => group.Values)
                    .ThenInclude(value => value.OptionValue)
            .SingleOrDefaultAsync(
                item =>
                    item.Id == id &&
                    !item.IsDeleted &&
                    item.IsVisible &&
                    !item.Category.IsDeleted &&
                    item.Category.IsVisible,
                cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Product not found",
                "PRODUCT_NOT_FOUND");

        return product.ToPublicDto(menuValidator, mediaStorage);
    }

    private PublicProductListItemDto MapListItem(
        ProductListProjection product,
        IEnumerable<ProductGroupProjection> productGroups,
        IReadOnlyCollection<ProductValueProjection> allValues)
    {
        var issues = new List<MenuIssueDto>();
        var priceFrom = product.BasePrice;

        if (!product.IsAvailable)
        {
            issues.Add(new MenuIssueDto(
                "PRODUCT_UNAVAILABLE",
                "The product is unavailable.",
                null));
        }

        foreach (var group in productGroups)
        {
            var validValues = allValues
                .Where(value =>
                    value.ProductOptionGroupId == group.Id &&
                    value.IsAvailable &&
                    value.OptionValueIsActive &&
                    !value.OptionValueIsDeleted &&
                    value.OptionValueGroupId == group.OptionGroupId)
                .ToArray();

            if (!group.OptionGroupIsActive || group.OptionGroupIsDeleted)
            {
                issues.Add(new MenuIssueDto(
                    "OPTION_GROUP_UNAVAILABLE",
                    "The assigned global option group is unavailable.",
                    group.Id));
            }

            if (group.IsRequired && validValues.Length == 0)
            {
                issues.Add(new MenuIssueDto(
                    "REQUIRED_OPTION_HAS_NO_AVAILABLE_VALUES",
                    "A required option group has no available valid values.",
                    group.Id));
            }

            if (group.IsRequired &&
                group.SelectionType == OptionSelectionType.Single)
            {
                if (!validValues.Any(value => value.IsDefault))
                {
                    issues.Add(new MenuIssueDto(
                        "REQUIRED_SINGLE_GROUP_HAS_NO_AVAILABLE_DEFAULT",
                        "A required single-selection group has no available default.",
                        group.Id));
                }

                if (group.OptionGroupIsActive &&
                    !group.OptionGroupIsDeleted &&
                    validValues.Length > 0)
                {
                    priceFrom += validValues.Min(value => value.PriceModifier);
                }
            }
        }

        return new PublicProductListItemDto(
            product.Id,
            product.CategoryId,
            product.Name,
            product.ShortDescription,
            product.ImageStorageKey is not null &&
            string.Equals(
                product.ImageStorageProvider,
                mediaStorage.ProviderName,
                StringComparison.OrdinalIgnoreCase)
                ? mediaStorage.GetPublicUrl(product.ImageStorageKey)
                : null,
            priceFrom,
            MenuDtoMapper.Currency,
            product.WeightGrams,
            product.VolumeMilliliters,
            product.Calories,
            product.IsAvailable,
            issues.Count == 0,
            issues);
    }

    private sealed record ProductListProjection(
        Guid Id,
        Guid CategoryId,
        string Name,
        string? ShortDescription,
        string? ImageStorageProvider,
        string? ImageStorageKey,
        decimal BasePrice,
        int? WeightGrams,
        int? VolumeMilliliters,
        int? Calories,
        bool IsAvailable);

    private sealed record ProductGroupProjection(
        Guid Id,
        Guid ProductId,
        Guid OptionGroupId,
        bool IsRequired,
        OptionSelectionType SelectionType,
        bool OptionGroupIsActive,
        bool OptionGroupIsDeleted);

    private sealed record ProductValueProjection(
        Guid ProductOptionGroupId,
        decimal PriceModifier,
        bool IsDefault,
        bool IsAvailable,
        Guid OptionValueGroupId,
        bool OptionValueIsActive,
        bool OptionValueIsDeleted);
}
