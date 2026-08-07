using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class AdminCategoryService(
    MoodPickupDbContext dbContext,
    IMenuConfigurationValidator menuValidator,
    IEmployeeAuditService auditService) : IAdminCategoryService
{
    public async Task<PagedResponse<AdminCategoryDto>> GetCategoriesAsync(
        AdminCategoryQuery query,
        CancellationToken cancellationToken)
    {
        var categories = dbContext.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(category => query.IncludeDeleted || !category.IsDeleted);

        var search = query.Search?.Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(search))
        {
            categories = categories.Where(category =>
                category.NormalizedName.Contains(search) ||
                (category.Description != null &&
                 category.Description.ToLower().Contains(search)));
        }

        var totalCount = await categories.CountAsync(cancellationToken);
        var items = await categories
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(category => new AdminCategoryDto(
                category.Id,
                category.Name,
                category.Description,
                category.DisplayOrder,
                category.IsVisible,
                category.IsDeleted,
                category.Products.Count(product => !product.IsDeleted),
                category.CreatedAt,
                category.UpdatedAt,
                category.RowVersion))
            .ToListAsync(cancellationToken);

        return new PagedResponse<AdminCategoryDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<AdminCategoryDto> GetCategoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var category = await dbContext.Categories
            .IgnoreQueryFilters()
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Category not found",
                "CATEGORY_NOT_FOUND");
        var productCount = await dbContext.Products
            .IgnoreQueryFilters()
            .CountAsync(
                product => product.CategoryId == id && !product.IsDeleted,
                cancellationToken);
        return category.ToAdminDto(productCount);
    }

    public async Task<AdminCategoryDto> CreateAsync(
        CreateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = request.Name,
            Description = request.Description,
            DisplayOrder = request.DisplayOrder,
            IsVisible = request.IsVisible
        };
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateCategory(category));

        dbContext.Categories.Add(category);
        await auditService.RecordAsync(
            "CategoryCreated",
            "Category",
            category.Id,
            $"Created category '{category.Name}'.",
            null,
            CategoryAuditValues(category),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.ToAdminDto(0);
    }

    public async Task<AdminCategoryDto> UpdateAsync(
        Guid id,
        UpdateCategoryRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetTrackedCategoryAsync(id, cancellationToken);
        EnsureNotDeleted(category);
        MenuServiceSupport.EnsureVersion(request.RowVersion, category.RowVersion, id);
        var oldValues = CategoryAuditValues(category);

        category.Name = request.Name;
        category.Description = request.Description;
        category.DisplayOrder = request.DisplayOrder;
        category.IsVisible = request.IsVisible;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateCategory(category));

        await auditService.RecordAsync(
            "CategoryUpdated",
            "Category",
            category.Id,
            $"Updated category '{category.Name}'.",
            oldValues,
            CategoryAuditValues(category),
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);

        return category.ToAdminDto(await CountProductsAsync(id, cancellationToken));
    }

    public async Task<IReadOnlyList<AdminCategoryDto>> ReorderAsync(
        ReorderCategoriesRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var ids = request.Items.Select(item => item.Id).ToArray();
        var categories = await dbContext.Categories
            .Where(category => ids.Contains(category.Id))
            .ToListAsync(cancellationToken);

        if (categories.Count != ids.Length)
        {
            throw MenuServiceSupport.NotFound(
                "One or more categories were not found",
                "CATEGORY_NOT_FOUND");
        }

        foreach (var item in request.Items)
        {
            var category = categories.Single(value => value.Id == item.Id);
            MenuServiceSupport.EnsureVersion(
                item.RowVersion,
                category.RowVersion,
                category.Id);
        }

        foreach (var item in request.Items)
        {
            var category = categories.Single(value => value.Id == item.Id);
            var oldOrder = category.DisplayOrder;
            category.DisplayOrder = item.DisplayOrder;
            await auditService.RecordAsync(
                "CategoryReordered",
                "Category",
                category.Id,
                $"Changed category '{category.Name}' display order.",
                new { displayOrder = oldOrder },
                new { displayOrder = category.DisplayOrder },
                cancellationToken);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var counts = await dbContext.Products
            .IgnoreQueryFilters()
            .Where(product => ids.Contains(product.CategoryId) && !product.IsDeleted)
            .GroupBy(product => product.CategoryId)
            .Select(group => new { CategoryId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.CategoryId, item => item.Count, cancellationToken);

        return categories
            .OrderBy(category => category.DisplayOrder)
            .ThenBy(category => category.Name)
            .Select(category => category.ToAdminDto(
                counts.GetValueOrDefault(category.Id)))
            .ToArray();
    }

    public async Task<AdminCategoryDto> SetVisibilityAsync(
        Guid id,
        SetVisibilityRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetTrackedCategoryAsync(id, cancellationToken);
        EnsureNotDeleted(category);
        MenuServiceSupport.EnsureVersion(request.RowVersion, category.RowVersion, id);
        var oldVisibility = category.IsVisible;
        category.IsVisible = request.IsVisible;

        await auditService.RecordAsync(
            "CategoryVisibilityChanged",
            "Category",
            category.Id,
            $"Changed visibility for category '{category.Name}'.",
            new { isVisible = oldVisibility },
            new { isVisible = category.IsVisible },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return category.ToAdminDto(await CountProductsAsync(id, cancellationToken));
    }

    public async Task DeleteAsync(
        Guid id,
        Guid rowVersion,
        CancellationToken cancellationToken)
    {
        var category = await GetTrackedCategoryAsync(id, cancellationToken);
        if (category.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Category is already deleted",
                "CATEGORY_ALREADY_DELETED");
        }

        MenuServiceSupport.EnsureVersion(rowVersion, category.RowVersion, id);
        category.IsDeleted = true;
        await auditService.RecordAsync(
            "CategoryDeleted",
            "Category",
            category.Id,
            $"Soft-deleted category '{category.Name}'.",
            new { isDeleted = false },
            new { isDeleted = true },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<AdminCategoryDto> RestoreAsync(
        Guid id,
        RowVersionRequest request,
        CancellationToken cancellationToken)
    {
        var category = await GetTrackedCategoryAsync(id, cancellationToken);
        MenuServiceSupport.EnsureVersion(request.RowVersion, category.RowVersion, id);
        category.IsDeleted = false;
        MenuServiceSupport.ThrowIfStructurallyInvalid(
            menuValidator.ValidateCategory(category));

        await auditService.RecordAsync(
            "CategoryRestored",
            "Category",
            category.Id,
            $"Restored category '{category.Name}'.",
            new { isDeleted = true },
            new { isDeleted = false },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return category.ToAdminDto(await CountProductsAsync(id, cancellationToken));
    }

    private async Task<Category> GetTrackedCategoryAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Categories
            .IgnoreQueryFilters()
            .SingleOrDefaultAsync(category => category.Id == id, cancellationToken)
            ?? throw MenuServiceSupport.NotFound(
                "Category not found",
                "CATEGORY_NOT_FOUND");
    }

    private async Task<int> CountProductsAsync(
        Guid categoryId,
        CancellationToken cancellationToken)
    {
        return await dbContext.Products
            .IgnoreQueryFilters()
            .CountAsync(
                product =>
                    product.CategoryId == categoryId &&
                    !product.IsDeleted,
                cancellationToken);
    }

    private static void EnsureNotDeleted(Category category)
    {
        if (category.IsDeleted)
        {
            throw MenuServiceSupport.Conflict(
                "Category is already deleted",
                "CATEGORY_ALREADY_DELETED");
        }
    }

    private static object CategoryAuditValues(Category category)
    {
        return new
        {
            category.Name,
            category.Description,
            category.DisplayOrder,
            category.IsVisible,
            category.IsDeleted
        };
    }
}
