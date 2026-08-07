using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class AdminProductService(
    MoodPickupDbContext dbContext,
    IMenuConfigurationValidator menuValidator,
    IMediaStorage mediaStorage,
    IEmployeeAuditService auditService) : IAdminProductService
{
    public async Task<PagedResponse<AdminProductListItemDto>> GetProductsAsync(
        AdminProductQuery query,
        CancellationToken cancellationToken)
    {
        var products = dbContext.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(product => query.IncludeDeleted || !product.IsDeleted);

        if (query.CategoryId is Guid categoryId)
        {
            products = products.Where(product => product.CategoryId == categoryId);
        }

        if (query.IsAvailable is bool isAvailable)
        {
            products = products.Where(product => product.IsAvailable == isAvailable);
        }

        if (query.IsVisible is bool isVisible)
        {
            products = products.Where(product => product.IsVisible == isVisible);
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
        var pageIds = await products
            .OrderBy(product => product.Category.DisplayOrder)
            .ThenBy(product => product.DisplayOrder)
            .ThenBy(product => product.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(product => product.Id)
            .ToListAsync(cancellationToken);
        var pageProducts = pageIds.Count == 0
            ? []
            : await dbContext.Products
                .IgnoreQueryFilters()
                .AsNoTracking()
                .Where(product => pageIds.Contains(product.Id))
                .Include(product => product.Category)
                .Include(product => product.Image)
                .Include(product => product.OptionGroups)
                    .ThenInclude(group => group.OptionGroup)
                .Include(product => product.OptionGroups)
                    .ThenInclude(group => group.Values)
                        .ThenInclude(value => value.OptionValue)
                .AsSplitQuery()
                .ToListAsync(cancellationToken);
        var productLookup = pageProducts.ToDictionary(product => product.Id);
        var items = pageIds
            .Select(id => productLookup[id].ToAdminListDto(
                menuValidator,
                mediaStorage))
            .ToArray();

        return new PagedResponse<AdminProductListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<AdminProductDto> GetProductAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(id, tracking: false, cancellationToken);
        return product.ToAdminDto(menuValidator, mediaStorage);
    }

    public async Task<MenuMutationResponse<AdminProductDto>> CreateAsync(
        CreateProductRequest request,
        CancellationToken cancellationToken)
    {
        var category = await RequireCategoryAsync(
            request.CategoryId,
            cancellationToken);
        var image = await ResolveImageAsync(request.ImageId, cancellationToken);
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = request.Name,
            ShortDescription = request.ShortDescription,
            Description = request.Description,
            Ingredients = request.Ingredients,
            BasePrice = request.BasePrice,
            DefaultWeightGrams = request.DefaultWeightGrams,
            DefaultVolumeMilliliters = request.DefaultVolumeMilliliters,
            DefaultCalories = request.DefaultCalories,
            ImageId = image?.Id,
            Image = image,
            IsAvailable = request.IsAvailable,
            IsVisible = request.IsVisible,
            DisplayOrder = request.DisplayOrder
        };
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProduct(product));

        dbContext.Products.Add(product);
        await auditService.RecordAsync(
            "ProductCreated",
            "Product",
            product.Id,
            $"Created product '{product.Name}'.",
            null,
            ProductAuditValues(product),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMutationResponse(product);
    }

    public async Task<MenuMutationResponse<AdminProductDto>> UpdateAsync(
        Guid id,
        UpdateProductRequest request,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(id, tracking: true, cancellationToken);
        EnsureNotDeleted(product);
        MenuServiceSupport.EnsureVersion(request.RowVersion, product.RowVersion, id);
        var category = await RequireCategoryAsync(
            request.CategoryId,
            cancellationToken);
        var image = await ResolveImageAsync(request.ImageId, cancellationToken);
        var oldValues = ProductAuditValues(product);

        product.CategoryId = category.Id;
        product.Category = category;
        product.Name = request.Name;
        product.ShortDescription = request.ShortDescription;
        product.Description = request.Description;
        product.Ingredients = request.Ingredients;
        product.BasePrice = request.BasePrice;
        product.DefaultWeightGrams = request.DefaultWeightGrams;
        product.DefaultVolumeMilliliters = request.DefaultVolumeMilliliters;
        product.DefaultCalories = request.DefaultCalories;
        product.ImageId = image?.Id;
        product.Image = image;
        product.IsAvailable = request.IsAvailable;
        product.IsVisible = request.IsVisible;
        product.DisplayOrder = request.DisplayOrder;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(product));

        await auditService.RecordAsync(
            "ProductUpdated",
            "Product",
            product.Id,
            $"Updated product '{product.Name}'.",
            oldValues,
            ProductAuditValues(product),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMutationResponse(product);
    }

    public async Task<MenuMutationResponse<AdminProductDto>> DuplicateAsync(
        Guid id,
        DuplicateProductRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var source = await LoadProductAsync(id, tracking: true, cancellationToken);
        EnsureNotDeleted(source);
        var duplicate = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = source.CategoryId,
            Category = source.Category,
            Name = string.IsNullOrWhiteSpace(request.Name)
                ? $"{source.Name} Copy"
                : request.Name,
            ShortDescription = source.ShortDescription,
            Description = source.Description,
            Ingredients = source.Ingredients,
            BasePrice = source.BasePrice,
            DefaultWeightGrams = source.DefaultWeightGrams,
            DefaultVolumeMilliliters = source.DefaultVolumeMilliliters,
            DefaultCalories = source.DefaultCalories,
            ImageId = source.ImageId,
            Image = source.Image,
            IsAvailable = source.IsAvailable,
            IsVisible = source.IsVisible,
            DisplayOrder = source.DisplayOrder
        };

        foreach (var sourceGroup in source.OptionGroups)
        {
            var group = new ProductOptionGroup
            {
                Id = Guid.NewGuid(),
                ProductId = duplicate.Id,
                Product = duplicate,
                OptionGroupId = sourceGroup.OptionGroupId,
                OptionGroup = sourceGroup.OptionGroup,
                IsRequired = sourceGroup.IsRequired,
                MinimumSelections = sourceGroup.MinimumSelections,
                MaximumSelections = sourceGroup.MaximumSelections,
                DisplayOrder = sourceGroup.DisplayOrder,
                IsActive = sourceGroup.IsActive
            };

            foreach (var sourceValue in sourceGroup.Values)
            {
                group.Values.Add(new ProductOptionValue
                {
                    Id = Guid.NewGuid(),
                    ProductOptionGroupId = group.Id,
                    ProductOptionGroup = group,
                    OptionValueId = sourceValue.OptionValueId,
                    OptionValue = sourceValue.OptionValue,
                    PriceModifier = sourceValue.PriceModifier,
                    IsDefault = sourceValue.IsDefault,
                    IsAvailable = sourceValue.IsAvailable,
                    DisplayOrder = sourceValue.DisplayOrder,
                    VolumeMilliliters = sourceValue.VolumeMilliliters,
                    Calories = sourceValue.Calories
                });
            }

            duplicate.OptionGroups.Add(group);
        }

        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(duplicate));
        dbContext.Products.Add(duplicate);
        await auditService.RecordAsync(
            "ProductDuplicated",
            "Product",
            duplicate.Id,
            $"Duplicated product '{source.Name}' as '{duplicate.Name}'.",
            new { sourceProductId = source.Id },
            ProductAuditValues(duplicate),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToMutationResponse(duplicate);
    }

    public async Task<IReadOnlyList<AdminProductListItemDto>> ReorderAsync(
        ReorderProductsRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await RequireCategoryAsync(request.CategoryId, cancellationToken);
        var ids = request.Items.Select(item => item.Id).ToArray();
        var products = await dbContext.Products
            .Include(product => product.Category)
            .Where(product => ids.Contains(product.Id))
            .ToListAsync(cancellationToken);

        if (products.Count != ids.Length)
        {
            throw MenuServiceSupport.NotFound(
                "One or more products were not found",
                "PRODUCT_NOT_FOUND");
        }

        if (products.Any(product => product.CategoryId != request.CategoryId))
        {
            throw MenuServiceSupport.Conflict(
                "Products must belong to the requested category",
                "MENU_CONFIGURATION_INVALID");
        }

        foreach (var item in request.Items)
        {
            var product = products.Single(value => value.Id == item.Id);
            MenuServiceSupport.EnsureVersion(
                item.RowVersion,
                product.RowVersion,
                product.Id);
        }

        foreach (var item in request.Items)
        {
            var product = products.Single(value => value.Id == item.Id);
            var oldOrder = product.DisplayOrder;
            product.DisplayOrder = item.DisplayOrder;
            await auditService.RecordAsync(
                "ProductReordered",
                "Product",
                product.Id,
                $"Changed product '{product.Name}' display order.",
                new { displayOrder = oldOrder },
                new { displayOrder = product.DisplayOrder },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return products
            .OrderBy(product => product.DisplayOrder)
            .ThenBy(product => product.Name)
            .Select(product => product.ToAdminListDto(
                menuValidator,
                mediaStorage))
            .ToArray();
    }

    public Task<MenuMutationResponse<AdminProductDto>> SetAvailabilityAsync(
        Guid id,
        SetAvailabilityRequest request,
        CancellationToken cancellationToken)
    {
        return UpdateFlagAsync(
            id,
            request.RowVersion,
            "ProductAvailabilityChanged",
            "availability",
            product => product.IsAvailable,
            (product, value) => product.IsAvailable = value,
            request.IsAvailable,
            cancellationToken);
    }

    public Task<MenuMutationResponse<AdminProductDto>> SetVisibilityAsync(
        Guid id,
        SetVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        return UpdateFlagAsync(
            id,
            request.RowVersion,
            "ProductVisibilityChanged",
            "visibility",
            product => product.IsVisible,
            (product, value) => product.IsVisible = value,
            request.IsVisible,
            cancellationToken);
    }

    public async Task<MenuMutationResponse<AdminProductDto>> AssignImageAsync(
        Guid id,
        AssignProductImageRequest request,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(id, tracking: true, cancellationToken);
        EnsureNotDeleted(product);
        MenuServiceSupport.EnsureVersion(request.RowVersion, product.RowVersion, id);
        var image = await ResolveImageAsync(request.ImageId, cancellationToken);
        var oldImageId = product.ImageId;
        product.ImageId = image?.Id;
        product.Image = image;

        await auditService.RecordAsync(
            "ProductImageChanged",
            "Product",
            product.Id,
            $"Changed image metadata reference for product '{product.Name}'.",
            new { imageId = oldImageId },
            new { imageId = product.ImageId },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMutationResponse(product);
    }

    public async Task DeleteAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(id, tracking: true, cancellationToken);
        if (product.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Product is already deleted",
                "PRODUCT_ALREADY_DELETED");
        }

        MenuServiceSupport.EnsureVersion(rowVersion, product.RowVersion, id);
        product.IsDeleted = true;
        await auditService.RecordAsync(
            "ProductDeleted",
            "Product",
            product.Id,
            $"Soft-deleted product '{product.Name}'.",
            new { isDeleted = false },
            new { isDeleted = true },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<MenuMutationResponse<AdminProductDto>> RestoreAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(id, tracking: true, cancellationToken);
        MenuServiceSupport.EnsureVersion(request.RowVersion, product.RowVersion, id);
        if (product.Category.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "A product cannot be restored into a deleted category",
                "MENU_CONFIGURATION_INVALID");
        }

        product.IsDeleted = false;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateProductConfiguration(product));
        await auditService.RecordAsync(
            "ProductRestored",
            "Product",
            product.Id,
            $"Restored product '{product.Name}'.",
            new { isDeleted = true },
            new { isDeleted = false },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMutationResponse(product);
    }

    private async Task<MenuMutationResponse<AdminProductDto>> UpdateFlagAsync(
        Guid id,
        Guid rowVersion,
        string actionType,
        string flagName,
        Func<Product, bool> getter,
        Action<Product, bool> setter,
        bool value,
        CancellationToken cancellationToken)
    {
        var product = await LoadProductAsync(id, tracking: true, cancellationToken);
        EnsureNotDeleted(product);
        MenuServiceSupport.EnsureVersion(rowVersion, product.RowVersion, id);
        var oldValue = getter(product);
        setter(product, value);

        await auditService.RecordAsync(
            actionType,
            "Product",
            product.Id,
            $"Changed {flagName} for product '{product.Name}'.",
            new Dictionary<string, bool> { [flagName] = oldValue },
            new Dictionary<string, bool> { [flagName] = value },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return ToMutationResponse(product);
    }

    private async Task<Product> LoadProductAsync(
        Guid id,
        bool tracking,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Products
            .IgnoreQueryFilters()
            .Include(product => product.Category)
            .Include(product => product.Image)
            .Include(product => product.OptionGroups)
                .ThenInclude(group => group.OptionGroup)
            .Include(product => product.OptionGroups)
                .ThenInclude(group => group.Values)
                    .ThenInclude(value => value.OptionValue)
            .AsSplitQuery();

        if (!tracking)
        {
            query = query.AsNoTracking();
        }

        return await query.SingleOrDefaultAsync(
                   product => product.Id == id,
                   cancellationToken)
               ?? throw MenuServiceSupport.NotFound(
                   "Product not found",
                   "PRODUCT_NOT_FOUND");
    }

    private async Task<Category> RequireCategoryAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .SingleOrDefaultAsync(category => category.Id == categoryId, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Category not found",
                "CATEGORY_NOT_FOUND");
    }

    private async Task<MediaFile?> ResolveImageAsync(
        Guid? imageId,
        CancellationToken cancellationToken)
    {
        if (imageId is null)
        {
            return null;
        }

        return await dbContext.MediaFiles
            .SingleOrDefaultAsync(media => media.Id == imageId, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Media file not found",
                "MEDIA_FILE_NOT_FOUND");
    }

    private MenuMutationResponse<AdminProductDto> ToMutationResponse(Product product)
    {
        return new MenuMutationResponse<AdminProductDto>(
            product.ToAdminDto(menuValidator, mediaStorage),
            menuValidator.EvaluateOrderability(product).ToDto());
    }

    private static void EnsureNotDeleted(Product product)
    {
        if (product.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Product is already deleted",
                "PRODUCT_ALREADY_DELETED");
        }
    }

    private static object ProductAuditValues(Product product)
    {
        return new
        {
            product.CategoryId,
            product.Name,
            product.ShortDescription,
            product.BasePrice,
            product.DefaultWeightGrams,
            product.DefaultVolumeMilliliters,
            product.DefaultCalories,
            product.ImageId,
            product.IsAvailable,
            product.IsVisible,
            product.IsDeleted,
            product.DisplayOrder
        };
    }
}
