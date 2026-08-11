using FluentValidation;
using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Validators;

public sealed class EmployeeListQueryValidator : PaginationValidator<EmployeeListQuery>
{
    public EmployeeListQueryValidator()
    {
        RuleFor(query => query.Search).MaximumLength(100);
        RuleFor(query => query.Role).MaximumLength(64);
        RuleFor(query => query.Status).IsInEnum();
    }
}

public sealed class CreateEmployeeRequestValidator
    : AbstractValidator<CreateEmployeeRequest>
{
    public CreateEmployeeRequestValidator()
    {
        EmployeeValidationRules.FullName(RuleFor(request => request.FullName));
        EmployeeValidationRules.Username(RuleFor(request => request.Username));
        EmployeeValidationRules.Roles(RuleFor(request => request.Roles));
    }
}

public sealed class UpdateEmployeeRequestValidator
    : AbstractValidator<UpdateEmployeeRequest>
{
    public UpdateEmployeeRequestValidator()
    {
        EmployeeValidationRules.FullName(RuleFor(request => request.FullName));
        EmployeeValidationRules.Username(RuleFor(request => request.Username));
        EmployeeValidationRules.Roles(RuleFor(request => request.Roles));
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class EmployeeVersionRequestValidator
    : AbstractValidator<EmployeeVersionRequest>
{
    public EmployeeVersionRequestValidator()
    {
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class ReplaceEmployeePermissionsRequestValidator
    : AbstractValidator<ReplaceEmployeePermissionsRequest>
{
    public ReplaceEmployeePermissionsRequestValidator()
    {
        RuleFor(request => request.Overrides)
            .Cascade(CascadeMode.Stop)
            .NotNull()
            .Must(overrides => overrides
                .Select(permission => permission.Permission)
                .Distinct(StringComparer.Ordinal)
                .Count() == overrides.Count)
            .WithMessage("Permission overrides cannot contain duplicates.");
        RuleForEach(request => request.Overrides)
            .SetValidator(new EmployeePermissionOverrideRequestValidator());
    }
}

public sealed class EmployeePermissionOverrideRequestValidator
    : AbstractValidator<EmployeePermissionOverrideRequest>
{
    public EmployeePermissionOverrideRequestValidator()
    {
        RuleFor(request => request.Permission)
            .NotEmpty()
            .Must(EmployeePermissionCatalog.IsKnown)
            .WithMessage("Permission is not recognized.");
    }
}

public sealed class EmployeeActionQueryValidator
    : PaginationValidator<EmployeeActionQuery>
{
    public EmployeeActionQueryValidator()
    {
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

internal static class EmployeeValidationRules
{
    public static void FullName<T>(IRuleBuilderInitial<T, string> rule)
    {
        rule
            .NotEmpty()
            .Must(value => value == value.Trim())
            .WithMessage("Full name must be trimmed.")
            .Must(value => value.Trim().Length >= 2)
            .WithMessage("Full name must contain at least 2 characters.")
            .MaximumLength(100);
    }

    public static void Username<T>(IRuleBuilderInitial<T, string> rule)
    {
        rule
            .NotEmpty()
            .Must(value => value == value.Trim())
            .WithMessage("Username must be trimmed.")
            .MinimumLength(3)
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9._-]+$")
            .WithMessage("Username may contain only letters, digits, periods, underscores, and hyphens.");
    }

    public static void Roles<T>(
        IRuleBuilderInitial<T, IReadOnlyList<string>> rule)
    {
        rule
            .NotNull()
            .NotEmpty()
            .Must(roles => roles.All(role => !string.IsNullOrWhiteSpace(role)))
            .WithMessage("Roles cannot contain empty values.")
            .Must(roles => roles.All(role => role == role.Trim()))
            .WithMessage("Roles must be trimmed.")
            .Must(roles => roles.Distinct(StringComparer.OrdinalIgnoreCase).Count() == roles.Count)
            .WithMessage("Roles cannot contain duplicates.");
        rule.ForEach(role => role.MaximumLength(64));
    }
}
