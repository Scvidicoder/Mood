using MoodPickup.Api.Entities;

namespace MoodPickup.Api.Interfaces;

public interface IMenuConfigurationValidator
{
    MenuValidationResult ValidateCategory(Category category);

    MenuValidationResult ValidateProduct(Product product);

    MenuValidationResult ValidateMediaFile(MediaFile mediaFile);

    MenuValidationResult ValidateOptionGroup(OptionGroup optionGroup);

    MenuValidationResult ValidateOptionValue(OptionValue optionValue);

    MenuValidationResult ValidateProductOptionGroup(ProductOptionGroup productOptionGroup);

    MenuValidationResult ValidateProductOptionValue(ProductOptionValue productOptionValue);

    MenuValidationResult ValidateProductConfiguration(Product product);

    MenuAvailabilityResult EvaluateOrderability(Product product);
}

public sealed record MenuIssue(
    string Code,
    string Message,
    Guid? ProductOptionGroupId = null);

public sealed record MenuValidationResult(IReadOnlyList<MenuIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}

public sealed record MenuAvailabilityResult(IReadOnlyList<MenuIssue> Issues)
{
    public bool IsOrderable => Issues.Count == 0;
}
