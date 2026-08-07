using FluentValidation;
using MoodPickup.Api.DTOs.Audit;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Menu.Admin;
using MoodPickup.Api.DTOs.Menu.Public;

namespace MoodPickup.Api.Validators;

public abstract class PaginationValidator<T> : AbstractValidator<T>
    where T : PaginationQuery
{
    protected PaginationValidator()
    {
        RuleFor(query => query.Page).GreaterThanOrEqualTo(1);
        RuleFor(query => query.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class PublicProductQueryValidator
    : PaginationValidator<PublicProductQuery>
{
    public PublicProductQueryValidator()
    {
        RuleFor(query => query.CategoryId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Category ID must be a non-empty GUID.");
        RuleFor(query => query.Search)
            .MaximumLength(200);
    }
}

public sealed class AdminCategoryQueryValidator
    : PaginationValidator<AdminCategoryQuery>
{
    public AdminCategoryQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(200);
    }
}

public sealed class AdminProductQueryValidator
    : PaginationValidator<AdminProductQuery>
{
    public AdminProductQueryValidator()
    {
        RuleFor(query => query.CategoryId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Category ID must be a non-empty GUID.");
        RuleFor(query => query.Search).MaximumLength(200);
    }
}

public sealed class AdminOptionGroupQueryValidator
    : PaginationValidator<AdminOptionGroupQuery>
{
    public AdminOptionGroupQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(200);
    }
}

public sealed class AuditLogQueryValidator : PaginationValidator<AuditLogQuery>
{
    public AuditLogQueryValidator()
    {
        RuleFor(query => query.EmployeeId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Employee ID must be a non-empty GUID.");
        RuleFor(query => query.EntityId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Entity ID must be a non-empty GUID.");
        RuleFor(query => query.ActionType).MaximumLength(80);
        RuleFor(query => query.EntityType).MaximumLength(80);
        RuleFor(query => query)
            .Must(query =>
                query.DateFrom is null ||
                query.DateTo is null ||
                query.DateFrom <= query.DateTo)
            .WithMessage("dateFrom cannot be later than dateTo.");
    }
}

public sealed class CreateCategoryRequestValidator
    : AbstractValidator<CreateCategoryRequest>
{
    public CreateCategoryRequestValidator()
    {
        MenuValidationRules.Name(RuleFor(request => request.Name), 120);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateCategoryRequestValidator
    : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        MenuValidationRules.Name(RuleFor(request => request.Name), 120);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class SetVisibilityRequestValidator
    : AbstractValidator<SetVisibilityRequest>
{
    public SetVisibilityRequestValidator()
    {
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class SetAvailabilityRequestValidator
    : AbstractValidator<SetAvailabilityRequest>
{
    public SetAvailabilityRequestValidator()
    {
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class RowVersionRequestValidator
    : AbstractValidator<RowVersionRequest>
{
    public RowVersionRequestValidator()
    {
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class ReorderCategoriesRequestValidator
    : AbstractValidator<ReorderCategoriesRequest>
{
    public ReorderCategoriesRequestValidator()
    {
        MenuValidationRules.ReorderItems(RuleFor(request => request.Items));
    }
}

public sealed class ReorderProductsRequestValidator
    : AbstractValidator<ReorderProductsRequest>
{
    public ReorderProductsRequestValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        MenuValidationRules.ReorderItems(RuleFor(request => request.Items));
    }
}

public sealed class CreateProductRequestValidator
    : AbstractValidator<CreateProductRequest>
{
    public CreateProductRequestValidator()
    {
        ConfigureProductRules();
    }

    private void ConfigureProductRules()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        MenuValidationRules.Name(RuleFor(request => request.Name), 160);
        RuleFor(request => request.ShortDescription).MaximumLength(300);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Ingredients).MaximumLength(1000);
        MenuValidationRules.Money(RuleFor(request => request.BasePrice));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.DefaultWeightGrams));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.DefaultVolumeMilliliters));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.DefaultCalories));
        RuleFor(request => request.ImageId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Image ID must be a non-empty GUID.");
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductRequestValidator
    : AbstractValidator<UpdateProductRequest>
{
    public UpdateProductRequestValidator()
    {
        RuleFor(request => request.CategoryId).NotEmpty();
        MenuValidationRules.Name(RuleFor(request => request.Name), 160);
        RuleFor(request => request.ShortDescription).MaximumLength(300);
        RuleFor(request => request.Description).MaximumLength(2000);
        RuleFor(request => request.Ingredients).MaximumLength(1000);
        MenuValidationRules.Money(RuleFor(request => request.BasePrice));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.DefaultWeightGrams));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.DefaultVolumeMilliliters));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.DefaultCalories));
        RuleFor(request => request.ImageId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Image ID must be a non-empty GUID.");
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class DuplicateProductRequestValidator
    : AbstractValidator<DuplicateProductRequest>
{
    public DuplicateProductRequestValidator()
    {
        RuleFor(request => request.Name)
            .Must(name => name is null || name == name.Trim())
            .WithMessage("Name must be trimmed.")
            .MaximumLength(160);
    }
}

public sealed class AssignProductImageRequestValidator
    : AbstractValidator<AssignProductImageRequest>
{
    public AssignProductImageRequestValidator()
    {
        RuleFor(request => request.ImageId)
            .Must(id => id is null || id != Guid.Empty)
            .WithMessage("Image ID must be a non-empty GUID.");
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class CreateOptionGroupRequestValidator
    : AbstractValidator<CreateOptionGroupRequest>
{
    public CreateOptionGroupRequestValidator()
    {
        MenuValidationRules.OptionGroupRules(this);
    }
}

public sealed class UpdateOptionGroupRequestValidator
    : AbstractValidator<UpdateOptionGroupRequest>
{
    public UpdateOptionGroupRequestValidator()
    {
        MenuValidationRules.OptionGroupRules(this);
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class SetActiveRequestValidator : AbstractValidator<SetActiveRequest>
{
    public SetActiveRequestValidator()
    {
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class CreateOptionValueRequestValidator
    : AbstractValidator<CreateOptionValueRequest>
{
    public CreateOptionValueRequestValidator()
    {
        MenuValidationRules.Name(RuleFor(request => request.Name), 120);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateOptionValueRequestValidator
    : AbstractValidator<UpdateOptionValueRequest>
{
    public UpdateOptionValueRequestValidator()
    {
        MenuValidationRules.Name(RuleFor(request => request.Name), 120);
        RuleFor(request => request.Description).MaximumLength(500);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class CreateProductOptionGroupRequestValidator
    : AbstractValidator<CreateProductOptionGroupRequest>
{
    public CreateProductOptionGroupRequestValidator()
    {
        RuleFor(request => request.OptionGroupId).NotEmpty();
        MenuValidationRules.SelectionRules(
            this,
            request => request.IsRequired,
            request => request.MinimumSelections,
            request => request.MaximumSelections);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
    }
}

public sealed class UpdateProductOptionGroupRequestValidator
    : AbstractValidator<UpdateProductOptionGroupRequest>
{
    public UpdateProductOptionGroupRequestValidator()
    {
        MenuValidationRules.SelectionRules(
            this,
            request => request.IsRequired,
            request => request.MinimumSelections,
            request => request.MaximumSelections);
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class CreateProductOptionValueRequestValidator
    : AbstractValidator<CreateProductOptionValueRequest>
{
    public CreateProductOptionValueRequestValidator()
    {
        RuleFor(request => request.OptionValueId).NotEmpty();
        MenuValidationRules.Money(RuleFor(request => request.PriceModifier));
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.VolumeMilliliters));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.Calories));
    }
}

public sealed class UpdateProductOptionValueRequestValidator
    : AbstractValidator<UpdateProductOptionValueRequest>
{
    public UpdateProductOptionValueRequestValidator()
    {
        MenuValidationRules.Money(RuleFor(request => request.PriceModifier));
        RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.VolumeMilliliters));
        MenuValidationRules.NullableNonNegative(
            RuleFor(request => request.Calories));
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

internal static class MenuValidationRules
{
    public static void Name<T>(
        IRuleBuilderInitial<T, string> rule,
        int maximumLength)
    {
        rule
            .NotEmpty()
            .Must(name => name == name.Trim())
            .WithMessage("Name must be trimmed.")
            .MaximumLength(maximumLength);
    }

    public static void Money<T>(IRuleBuilderInitial<T, decimal> rule)
    {
        rule
            .GreaterThanOrEqualTo(0)
            .Must(value => decimal.Round(value, 2) == value)
            .WithMessage("Value cannot have more than two decimal places.");
    }

    public static void NullableNonNegative<T>(
        IRuleBuilderInitial<T, int?> rule)
    {
        rule
            .Must(value => value is null or >= 0)
            .WithMessage("Value cannot be negative.");
    }

    public static void ReorderItems<T>(
        IRuleBuilderInitial<T, IReadOnlyList<ReorderItemRequest>> rule)
    {
        rule
            .NotEmpty()
            .Must(items => items.Select(item => item.Id).Distinct().Count() == items.Count)
            .WithMessage("Reorder items cannot contain duplicate IDs.");
        rule.ForEach(item =>
        {
            item.ChildRules(child =>
            {
                child.RuleFor(value => value.Id).NotEmpty();
                child.RuleFor(value => value.DisplayOrder).GreaterThanOrEqualTo(0);
                child.RuleFor(value => value.RowVersion).NotEmpty();
            });
        });
    }

    public static void OptionGroupRules<T>(AbstractValidator<T> validator)
        where T : class
    {
        if (validator is AbstractValidator<CreateOptionGroupRequest> create)
        {
            Name(create.RuleFor(request => request.Name), 120);
            create.RuleFor(request => request.Description).MaximumLength(500);
            create.RuleFor(request => request.SelectionType).IsInEnum();
            create.RuleFor(request => request.DefaultMinimumSelections)
                .GreaterThanOrEqualTo(0);
            create.RuleFor(request => request.DefaultMaximumSelections)
                .Must(value => value is null or >= 1);
            create.RuleFor(request => request)
                .Must(HasValidSelectionRules)
                .WithMessage("Option selection rules are invalid.");
            create.RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        }
        else if (validator is AbstractValidator<UpdateOptionGroupRequest> update)
        {
            Name(update.RuleFor(request => request.Name), 120);
            update.RuleFor(request => request.Description).MaximumLength(500);
            update.RuleFor(request => request.SelectionType).IsInEnum();
            update.RuleFor(request => request.DefaultMinimumSelections)
                .GreaterThanOrEqualTo(0);
            update.RuleFor(request => request.DefaultMaximumSelections)
                .Must(value => value is null or >= 1);
            update.RuleFor(request => request)
                .Must(HasValidSelectionRules)
                .WithMessage("Option selection rules are invalid.");
            update.RuleFor(request => request.DisplayOrder).GreaterThanOrEqualTo(0);
        }
    }

    public static void SelectionRules<T>(
        AbstractValidator<T> validator,
        Func<T, bool> isRequired,
        Func<T, int> minimum,
        Func<T, int> maximum)
    {
        validator.RuleFor(request => request)
            .Must(request =>
                minimum(request) >= 0 &&
                maximum(request) >= 1 &&
                minimum(request) <= maximum(request) &&
                (!isRequired(request) || minimum(request) >= 1))
            .WithMessage("Option selection rules are invalid.");
    }

    private static bool HasValidSelectionRules(CreateOptionGroupRequest request)
    {
        return HasValidSelectionRules(
            request.SelectionType,
            request.DefaultIsRequired,
            request.DefaultMinimumSelections,
            request.DefaultMaximumSelections);
    }

    private static bool HasValidSelectionRules(UpdateOptionGroupRequest request)
    {
        return HasValidSelectionRules(
            request.SelectionType,
            request.DefaultIsRequired,
            request.DefaultMinimumSelections,
            request.DefaultMaximumSelections);
    }

    private static bool HasValidSelectionRules(
        Entities.OptionSelectionType selectionType,
        bool isRequired,
        int minimum,
        int? maximum)
    {
        return minimum >= 0 &&
               maximum is null or >= 1 &&
               (maximum is null || minimum <= maximum) &&
               (!isRequired || minimum >= 1) &&
               (selectionType != Entities.OptionSelectionType.Single ||
                maximum is null or <= 1);
    }
}
