using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.DTOs.Menu.Public;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

internal static class MenuDtoMapper
{
    public const string Currency = "TJS";

    public static MenuIssueDto ToDto(this MenuIssue issue)
    {
        return new MenuIssueDto(
            issue.Code,
            issue.Message,
            issue.ProductOptionGroupId);
    }

    public static OrderabilityDto ToDto(this MenuAvailabilityResult result)
    {
        return new OrderabilityDto(
            result.IsOrderable,
            result.Issues.Select(ToDto).ToArray());
    }

    public static AdminCategoryDto ToAdminDto(this Category category, int productCount)
    {
        return new AdminCategoryDto(
            category.Id,
            category.Name,
            category.Description,
            category.DisplayOrder,
            category.IsVisible,
            category.IsDeleted,
            productCount,
            category.CreatedAt,
            category.UpdatedAt,
            category.RowVersion);
    }

    public static AdminProductListItemDto ToAdminListDto(
        this Product product,
        IMenuConfigurationValidator validator,
        IMediaStorage mediaStorage)
    {
        var orderability = validator.EvaluateOrderability(product);
        return new AdminProductListItemDto(
            product.Id,
            product.CategoryId,
            product.Category.Name,
            product.Name,
            GetMediaUrl(product.Image, mediaStorage),
            product.BasePrice,
            product.IsAvailable,
            product.IsVisible,
            product.IsDeleted,
            orderability.IsOrderable,
            orderability.Issues.Select(ToDto).ToArray(),
            product.DisplayOrder,
            product.UpdatedAt,
            product.RowVersion);
    }

    public static AdminProductDto ToAdminDto(
        this Product product,
        IMenuConfigurationValidator validator,
        IMediaStorage mediaStorage)
    {
        return new AdminProductDto(
            product.Id,
            product.CategoryId,
            product.Category.Name,
            product.Name,
            product.ShortDescription,
            product.Description,
            product.Ingredients,
            product.BasePrice,
            product.DefaultWeightGrams,
            product.DefaultVolumeMilliliters,
            product.DefaultCalories,
            product.ImageId,
            product.Image is null
                ? null
                : new AdminMediaFileDto(
                    product.Image.Id,
                    product.Image.StorageProvider,
                    product.Image.StorageKey,
                    product.Image.OriginalFileName,
                    product.Image.ContentType,
                    product.Image.FileSizeBytes,
                    product.Image.Width,
                    product.Image.Height,
                    GetMediaUrl(product.Image, mediaStorage),
                    product.Image.IsDeleted),
            product.IsAvailable,
            product.IsVisible,
            product.IsDeleted,
            product.DisplayOrder,
            product.CreatedAt,
            product.UpdatedAt,
            product.RowVersion,
            validator.EvaluateOrderability(product).ToDto(),
            product.OptionGroups
                .OrderBy(group => group.DisplayOrder)
                .ThenBy(group => group.OptionGroup.Name)
                .Select(ToAdminDto)
                .ToArray());
    }

    public static AdminOptionGroupDto ToAdminDto(this OptionGroup optionGroup)
    {
        return new AdminOptionGroupDto(
            optionGroup.Id,
            optionGroup.Name,
            optionGroup.Description,
            optionGroup.SelectionType,
            optionGroup.DefaultIsRequired,
            optionGroup.DefaultMinimumSelections,
            optionGroup.DefaultMaximumSelections,
            optionGroup.DisplayOrder,
            optionGroup.IsActive,
            optionGroup.IsDeleted,
            optionGroup.CreatedAt,
            optionGroup.UpdatedAt,
            optionGroup.RowVersion,
            optionGroup.Values
                .OrderBy(value => value.DisplayOrder)
                .ThenBy(value => value.Name)
                .Select(ToAdminDto)
                .ToArray());
    }

    public static AdminOptionValueDto ToAdminDto(this OptionValue optionValue)
    {
        return new AdminOptionValueDto(
            optionValue.Id,
            optionValue.OptionGroupId,
            optionValue.Name,
            optionValue.Description,
            optionValue.DisplayOrder,
            optionValue.IsActive,
            optionValue.IsDeleted,
            optionValue.CreatedAt,
            optionValue.UpdatedAt,
            optionValue.RowVersion);
    }

    public static AdminProductOptionGroupDto ToAdminDto(
        this ProductOptionGroup assignment)
    {
        return new AdminProductOptionGroupDto(
            assignment.Id,
            assignment.OptionGroupId,
            assignment.OptionGroup.Name,
            assignment.OptionGroup.SelectionType,
            assignment.IsRequired,
            assignment.MinimumSelections,
            assignment.MaximumSelections,
            assignment.DisplayOrder,
            assignment.IsActive,
            assignment.OptionGroup.IsActive,
            assignment.OptionGroup.IsDeleted,
            assignment.CreatedAt,
            assignment.UpdatedAt,
            assignment.RowVersion,
            assignment.Values
                .OrderBy(value => value.DisplayOrder)
                .ThenBy(value => value.OptionValue.Name)
                .Select(ToAdminDto)
                .ToArray());
    }

    public static AdminProductOptionValueDto ToAdminDto(
        this ProductOptionValue assignment)
    {
        return new AdminProductOptionValueDto(
            assignment.Id,
            assignment.OptionValueId,
            assignment.OptionValue.Name,
            assignment.PriceModifier,
            assignment.IsDefault,
            assignment.IsAvailable,
            assignment.DisplayOrder,
            assignment.VolumeMilliliters,
            assignment.Calories,
            assignment.OptionValue.IsActive,
            assignment.OptionValue.IsDeleted,
            assignment.CreatedAt,
            assignment.UpdatedAt,
            assignment.RowVersion);
    }

    public static PublicProductDetailDto ToPublicDto(
        this Product product,
        IMenuConfigurationValidator validator,
        IMediaStorage mediaStorage)
    {
        var orderability = validator.EvaluateOrderability(product);
        var groups = product.OptionGroups
            .Where(assignment =>
                assignment.IsActive &&
                assignment.OptionGroup.IsActive &&
                !assignment.OptionGroup.IsDeleted)
            .OrderBy(assignment => assignment.DisplayOrder)
            .ThenBy(assignment => assignment.OptionGroup.Name)
            .Select(assignment => new PublicProductOptionGroupDto(
                assignment.Id,
                assignment.OptionGroup.Name,
                assignment.OptionGroup.Description,
                assignment.OptionGroup.SelectionType.ToString(),
                assignment.IsRequired,
                assignment.MinimumSelections,
                assignment.MaximumSelections,
                assignment.DisplayOrder,
                assignment.Values
                    .Where(value =>
                        value.OptionValue.IsActive &&
                        !value.OptionValue.IsDeleted)
                    .OrderBy(value => value.DisplayOrder)
                    .ThenBy(value => value.OptionValue.Name)
                    .Select(value => new PublicProductOptionValueDto(
                        value.Id,
                        value.OptionValueId,
                        value.OptionValue.Name,
                        value.OptionValue.Description,
                        value.PriceModifier,
                        value.IsDefault,
                        value.IsAvailable,
                        value.DisplayOrder,
                        value.VolumeMilliliters,
                        value.Calories))
                    .ToArray()))
            .ToArray();

        return new PublicProductDetailDto(
            product.Id,
            product.CategoryId,
            product.Name,
            product.Description,
            product.Ingredients,
            GetMediaUrl(product.Image, mediaStorage),
            product.BasePrice,
            CalculatePriceFrom(product),
            Currency,
            product.DefaultWeightGrams,
            product.DefaultVolumeMilliliters,
            product.DefaultCalories,
            product.IsAvailable,
            orderability.IsOrderable,
            orderability.Issues.Select(ToDto).ToArray(),
            groups);
    }

    public static decimal CalculatePriceFrom(Product product)
    {
        var result = product.BasePrice;

        foreach (var assignment in product.OptionGroups.Where(group =>
                     group.IsActive &&
                     group.IsRequired &&
                     group.OptionGroup.IsActive &&
                     !group.OptionGroup.IsDeleted &&
                     group.OptionGroup.SelectionType == OptionSelectionType.Single))
        {
            var modifiers = assignment.Values
                .Where(value =>
                    value.IsAvailable &&
                    value.OptionValue.IsActive &&
                    !value.OptionValue.IsDeleted &&
                    value.OptionValue.OptionGroupId == assignment.OptionGroupId)
                .Select(value => value.PriceModifier)
                .ToArray();

            if (modifiers.Length > 0)
            {
                result += modifiers.Min();
            }
        }

        return result;
    }

    public static string? GetMediaUrl(
        MediaFile? media,
        IMediaStorage mediaStorage)
    {
        return media is null ||
               media.IsDeleted ||
               !string.Equals(
                   media.StorageProvider,
                   mediaStorage.ProviderName,
                   StringComparison.OrdinalIgnoreCase)
            ? null
            : mediaStorage.GetPublicUrl(media.StorageKey);
    }
}
