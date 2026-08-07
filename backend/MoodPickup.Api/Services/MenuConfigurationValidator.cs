using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class MenuConfigurationValidator : IMenuConfigurationValidator
{
    public MenuValidationResult ValidateCategory(Category category)
    {
        var issues = new List<MenuIssue>();
        ValidateName(category.Name, 120, issues);
        AddNonNegativeIssue(
            category.DisplayOrder,
            "CATEGORY_DISPLAY_ORDER_NEGATIVE",
            "Category display order cannot be negative.",
            issues);
        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateProduct(Product product)
    {
        var issues = new List<MenuIssue>();
        if (product.CategoryId == Guid.Empty)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_CATEGORY_REQUIRED",
                "A product must belong to a category."));
        }

        ValidateName(product.Name, 160, issues);
        AddNonNegativeIssue(
            product.BasePrice,
            "PRODUCT_BASE_PRICE_NEGATIVE",
            "Product base price cannot be negative.",
            issues);
        AddNullableNonNegativeIssue(
            product.DefaultWeightGrams,
            "PRODUCT_WEIGHT_NEGATIVE",
            "Product weight cannot be negative.",
            issues);
        AddNullableNonNegativeIssue(
            product.DefaultVolumeMilliliters,
            "PRODUCT_VOLUME_NEGATIVE",
            "Product volume cannot be negative.",
            issues);
        AddNullableNonNegativeIssue(
            product.DefaultCalories,
            "PRODUCT_CALORIES_NEGATIVE",
            "Product calories cannot be negative.",
            issues);
        AddNonNegativeIssue(
            product.DisplayOrder,
            "PRODUCT_DISPLAY_ORDER_NEGATIVE",
            "Product display order cannot be negative.",
            issues);
        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateMediaFile(MediaFile mediaFile)
    {
        var issues = new List<MenuIssue>();
        if (string.IsNullOrWhiteSpace(mediaFile.StorageProvider))
        {
            issues.Add(new MenuIssue(
                "MEDIA_STORAGE_PROVIDER_REQUIRED",
                "Media storage provider is required."));
        }

        if (string.IsNullOrWhiteSpace(mediaFile.StorageKey))
        {
            issues.Add(new MenuIssue(
                "MEDIA_STORAGE_KEY_REQUIRED",
                "Media storage key is required."));
        }

        AddNonNegativeIssue(
            mediaFile.FileSizeBytes,
            "MEDIA_FILE_SIZE_NEGATIVE",
            "Media file size cannot be negative.",
            issues);
        AddNullablePositiveIssue(
            mediaFile.Width,
            "MEDIA_WIDTH_NOT_POSITIVE",
            "Media width must be positive when specified.",
            issues);
        AddNullablePositiveIssue(
            mediaFile.Height,
            "MEDIA_HEIGHT_NOT_POSITIVE",
            "Media height must be positive when specified.",
            issues);
        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateOptionGroup(OptionGroup optionGroup)
    {
        var issues = new List<MenuIssue>();
        ValidateName(optionGroup.Name, 120, issues);
        AddNonNegativeIssue(
            optionGroup.DefaultMinimumSelections,
            "OPTION_GROUP_MINIMUM_NEGATIVE",
            "Option-group minimum selections cannot be negative.",
            issues);

        if (optionGroup.DefaultMaximumSelections is <= 0)
        {
            issues.Add(new MenuIssue(
                "OPTION_GROUP_MAXIMUM_NOT_POSITIVE",
                "Option-group maximum selections must be positive when specified."));
        }

        if (optionGroup.DefaultMaximumSelections is int maximum &&
            optionGroup.DefaultMinimumSelections > maximum)
        {
            issues.Add(new MenuIssue(
                "OPTION_GROUP_MINIMUM_EXCEEDS_MAXIMUM",
                "Option-group minimum selections cannot exceed the maximum."));
        }

        if (optionGroup.SelectionType == OptionSelectionType.Single &&
            optionGroup.DefaultMaximumSelections is > 1)
        {
            issues.Add(new MenuIssue(
                "SINGLE_OPTION_GROUP_MAXIMUM_EXCEEDS_ONE",
                "A single-selection option group cannot allow more than one selection."));
        }

        if (optionGroup.DefaultIsRequired &&
            optionGroup.DefaultMinimumSelections < 1)
        {
            issues.Add(new MenuIssue(
                "REQUIRED_OPTION_GROUP_MINIMUM_ZERO",
                "A required option group must require at least one selection."));
        }

        AddNonNegativeIssue(
            optionGroup.DisplayOrder,
            "OPTION_GROUP_DISPLAY_ORDER_NEGATIVE",
            "Option-group display order cannot be negative.",
            issues);
        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateOptionValue(OptionValue optionValue)
    {
        var issues = new List<MenuIssue>();
        if (optionValue.OptionGroupId == Guid.Empty)
        {
            issues.Add(new MenuIssue(
                "OPTION_VALUE_GROUP_REQUIRED",
                "An option value must belong to an option group."));
        }

        ValidateName(optionValue.Name, 120, issues);
        AddNonNegativeIssue(
            optionValue.DisplayOrder,
            "OPTION_VALUE_DISPLAY_ORDER_NEGATIVE",
            "Option-value display order cannot be negative.",
            issues);
        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateProductOptionGroup(
        ProductOptionGroup productOptionGroup)
    {
        var issues = new List<MenuIssue>();
        Guid? assignmentId = productOptionGroup.Id == Guid.Empty
            ? null
            : productOptionGroup.Id;

        if (productOptionGroup.ProductId == Guid.Empty)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_GROUP_PRODUCT_REQUIRED",
                "A product option group must belong to a product.",
                assignmentId));
        }

        if (productOptionGroup.OptionGroupId == Guid.Empty)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_GROUP_GLOBAL_GROUP_REQUIRED",
                "A product option group must reference a global option group.",
                assignmentId));
        }

        if (productOptionGroup.MinimumSelections < 0)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_GROUP_MINIMUM_NEGATIVE",
                "Product option-group minimum selections cannot be negative.",
                assignmentId));
        }

        if (productOptionGroup.MaximumSelections < 1)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_GROUP_MAXIMUM_NOT_POSITIVE",
                "Product option-group maximum selections must be positive.",
                assignmentId));
        }

        if (productOptionGroup.MinimumSelections >
            productOptionGroup.MaximumSelections)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_GROUP_MINIMUM_EXCEEDS_MAXIMUM",
                "Product option-group minimum selections cannot exceed the maximum.",
                assignmentId));
        }

        if (productOptionGroup.IsRequired &&
            productOptionGroup.MinimumSelections < 1)
        {
            issues.Add(new MenuIssue(
                "REQUIRED_PRODUCT_OPTION_GROUP_MINIMUM_ZERO",
                "A required product option group must require at least one selection.",
                assignmentId));
        }

        if (productOptionGroup.OptionGroup is not null &&
            productOptionGroup.OptionGroup.SelectionType == OptionSelectionType.Single &&
            productOptionGroup.MaximumSelections != 1)
        {
            issues.Add(new MenuIssue(
                "SINGLE_PRODUCT_OPTION_GROUP_MAXIMUM_NOT_ONE",
                "A single-selection product option group must have a maximum of one.",
                assignmentId));
        }

        if (productOptionGroup.DisplayOrder < 0)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_GROUP_DISPLAY_ORDER_NEGATIVE",
                "Product option-group display order cannot be negative.",
                assignmentId));
        }

        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateProductOptionValue(
        ProductOptionValue productOptionValue)
    {
        var issues = new List<MenuIssue>();
        Guid? assignmentId = productOptionValue.ProductOptionGroupId == Guid.Empty
            ? null
            : productOptionValue.ProductOptionGroupId;

        if (productOptionValue.ProductOptionGroupId == Guid.Empty)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_VALUE_GROUP_REQUIRED",
                "A product option value must belong to a product option group."));
        }

        if (productOptionValue.OptionValueId == Guid.Empty)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_OPTION_VALUE_GLOBAL_VALUE_REQUIRED",
                "A product option value must reference a global option value.",
                assignmentId));
        }

        AddNonNegativeIssue(
            productOptionValue.PriceModifier,
            "PRODUCT_OPTION_VALUE_PRICE_NEGATIVE",
            "A product option price modifier cannot be negative.",
            issues,
            assignmentId);
        AddNonNegativeIssue(
            productOptionValue.DisplayOrder,
            "PRODUCT_OPTION_VALUE_DISPLAY_ORDER_NEGATIVE",
            "Product option-value display order cannot be negative.",
            issues,
            assignmentId);
        AddNullableNonNegativeIssue(
            productOptionValue.VolumeMilliliters,
            "PRODUCT_OPTION_VALUE_VOLUME_NEGATIVE",
            "Product option-value volume cannot be negative.",
            issues,
            assignmentId);
        AddNullableNonNegativeIssue(
            productOptionValue.Calories,
            "PRODUCT_OPTION_VALUE_CALORIES_NEGATIVE",
            "Product option-value calories cannot be negative.",
            issues,
            assignmentId);

        if (productOptionValue.ProductOptionGroup is not null &&
            productOptionValue.OptionValue is not null &&
            productOptionValue.OptionValue.OptionGroupId !=
            productOptionValue.ProductOptionGroup.OptionGroupId)
        {
            issues.Add(new MenuIssue(
                "OPTION_VALUE_BELONGS_TO_DIFFERENT_GROUP",
                "The global option value does not belong to the assigned option group.",
                assignmentId));
        }

        return new MenuValidationResult(issues);
    }

    public MenuValidationResult ValidateProductConfiguration(Product product)
    {
        var issues = new List<MenuIssue>();
        issues.AddRange(ValidateProduct(product).Issues);

        var duplicateGroups = product.OptionGroups
            .GroupBy(assignment => assignment.OptionGroupId)
            .Where(group => group.Count() > 1);

        foreach (var duplicateGroup in duplicateGroups)
        {
            issues.Add(new MenuIssue(
                "DUPLICATE_PRODUCT_OPTION_GROUP",
                "The same option group cannot be assigned to a product more than once."));
        }

        foreach (var productOptionGroup in product.OptionGroups)
        {
            issues.AddRange(ValidateProductOptionGroup(productOptionGroup).Issues);

            var duplicateValues = productOptionGroup.Values
                .GroupBy(assignment => assignment.OptionValueId)
                .Where(group => group.Count() > 1);

            foreach (var duplicateValue in duplicateValues)
            {
                issues.Add(new MenuIssue(
                    "DUPLICATE_PRODUCT_OPTION_VALUE",
                    "The same option value cannot be assigned to a product option group more than once.",
                    productOptionGroup.Id));
            }

            foreach (var productOptionValue in productOptionGroup.Values)
            {
                issues.AddRange(ValidateProductOptionValue(productOptionValue).Issues);
            }

            if (productOptionGroup.OptionGroup is not null &&
                productOptionGroup.OptionGroup.SelectionType ==
                OptionSelectionType.Single &&
                productOptionGroup.Values.Count(value => value.IsDefault) > 1)
            {
                issues.Add(new MenuIssue(
                    "SINGLE_GROUP_HAS_MULTIPLE_DEFAULTS",
                    "A single-selection product option group can have at most one default.",
                    productOptionGroup.Id));
            }
        }

        return new MenuValidationResult(issues);
    }

    public MenuAvailabilityResult EvaluateOrderability(Product product)
    {
        var issues = new List<MenuIssue>();
        issues.AddRange(ValidateProductConfiguration(product).Issues);

        if (product.IsDeleted)
        {
            issues.Add(new MenuIssue("PRODUCT_DELETED", "The product is deleted."));
        }

        if (!product.IsVisible)
        {
            issues.Add(new MenuIssue("PRODUCT_HIDDEN", "The product is hidden."));
        }

        if (!product.IsAvailable)
        {
            issues.Add(new MenuIssue("PRODUCT_UNAVAILABLE", "The product is unavailable."));
        }

        if (product.Category is null)
        {
            issues.Add(new MenuIssue(
                "PRODUCT_CATEGORY_MISSING",
                "The product category was not loaded or does not exist."));
        }
        else
        {
            if (product.Category.IsDeleted)
            {
                issues.Add(new MenuIssue(
                    "CATEGORY_DELETED",
                    "The product category is deleted."));
            }

            if (!product.Category.IsVisible)
            {
                issues.Add(new MenuIssue(
                    "CATEGORY_HIDDEN",
                    "The product category is hidden."));
            }
        }

        foreach (var productOptionGroup in product.OptionGroups.Where(group => group.IsActive))
        {
            var globalGroup = productOptionGroup.OptionGroup;
            var validAvailableValues = productOptionGroup.Values
                .Where(value =>
                    value.IsAvailable &&
                    value.OptionValue is not null &&
                    value.OptionValue.IsActive &&
                    !value.OptionValue.IsDeleted &&
                    value.OptionValue.OptionGroupId == productOptionGroup.OptionGroupId)
                .ToList();

            if (globalGroup is null || globalGroup.IsDeleted || !globalGroup.IsActive)
            {
                issues.Add(new MenuIssue(
                    "OPTION_GROUP_UNAVAILABLE",
                    "The assigned global option group is unavailable.",
                    productOptionGroup.Id));
            }

            if (productOptionGroup.IsRequired && validAvailableValues.Count == 0)
            {
                issues.Add(new MenuIssue(
                    "REQUIRED_OPTION_HAS_NO_AVAILABLE_VALUES",
                    "A required option group has no available valid values.",
                    productOptionGroup.Id));
            }

            if (productOptionGroup.IsRequired &&
                globalGroup?.SelectionType == OptionSelectionType.Single &&
                validAvailableValues.Count(value => value.IsDefault) == 0)
            {
                issues.Add(new MenuIssue(
                    "REQUIRED_SINGLE_GROUP_HAS_NO_AVAILABLE_DEFAULT",
                    "A required single-selection group has no available default.",
                    productOptionGroup.Id));
            }
        }

        return new MenuAvailabilityResult(issues);
    }

    private static void ValidateName(
        string name,
        int maximumLength,
        ICollection<MenuIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            issues.Add(new MenuIssue("NAME_REQUIRED", "Name is required."));
            return;
        }

        if (!string.Equals(name, name.Trim(), StringComparison.Ordinal))
        {
            issues.Add(new MenuIssue("NAME_NOT_TRIMMED", "Name must be trimmed."));
        }

        if (name.Length > maximumLength)
        {
            issues.Add(new MenuIssue(
                "NAME_TOO_LONG",
                $"Name cannot exceed {maximumLength} characters."));
        }
    }

    private static void AddNonNegativeIssue(
        int value,
        string code,
        string message,
        ICollection<MenuIssue> issues,
        Guid? productOptionGroupId = null)
    {
        if (value < 0)
        {
            issues.Add(new MenuIssue(code, message, productOptionGroupId));
        }
    }

    private static void AddNonNegativeIssue(
        decimal value,
        string code,
        string message,
        ICollection<MenuIssue> issues,
        Guid? productOptionGroupId = null)
    {
        if (value < 0)
        {
            issues.Add(new MenuIssue(code, message, productOptionGroupId));
        }
    }

    private static void AddNonNegativeIssue(
        long value,
        string code,
        string message,
        ICollection<MenuIssue> issues,
        Guid? productOptionGroupId = null)
    {
        if (value < 0)
        {
            issues.Add(new MenuIssue(code, message, productOptionGroupId));
        }
    }

    private static void AddNullableNonNegativeIssue(
        int? value,
        string code,
        string message,
        ICollection<MenuIssue> issues,
        Guid? productOptionGroupId = null)
    {
        if (value < 0)
        {
            issues.Add(new MenuIssue(code, message, productOptionGroupId));
        }
    }

    private static void AddNullablePositiveIssue(
        int? value,
        string code,
        string message,
        ICollection<MenuIssue> issues)
    {
        if (value is <= 0)
        {
            issues.Add(new MenuIssue(code, message));
        }
    }
}
