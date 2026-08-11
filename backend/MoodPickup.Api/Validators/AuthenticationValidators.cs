using FluentValidation;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Validators;

public sealed class RequestCustomerCodeRequestValidator
    : AbstractValidator<RequestCustomerCodeRequest>
{
    public RequestCustomerCodeRequestValidator()
    {
        RuleFor(request => request.PhoneNumber)
            .NotEmpty()
            .Must(phoneNumber =>
                PhoneNumberNormalizer.TryNormalize(phoneNumber, out _))
            .WithMessage("Phone number must use normalized international format.");
    }
}

public sealed class CustomerChallengeStatusRequestValidator
    : AbstractValidator<CustomerChallengeStatusRequest>
{
    public CustomerChallengeStatusRequestValidator()
    {
        RuleFor(request => request.ChallengeId).NotEmpty();
        RuleFor(request => request.ClientChallengeSecret)
            .NotEmpty()
            .MaximumLength(128);
    }
}

public sealed class VerifyCustomerCodeRequestValidator
    : AbstractValidator<VerifyCustomerCodeRequest>
{
    public VerifyCustomerCodeRequestValidator()
    {
        RuleFor(request => request.ChallengeId).NotEmpty();
        RuleFor(request => request.Code)
            .NotEmpty()
            .Matches(@"^\d{6}$")
            .WithMessage("Code must contain exactly six digits.");
    }
}

public sealed class CompleteCustomerRegistrationRequestValidator
    : AbstractValidator<CompleteCustomerRegistrationRequest>
{
    public CompleteCustomerRegistrationRequestValidator()
    {
        RuleFor(request => request.RegistrationToken).NotEmpty();
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);
    }
}

public sealed class UpdateCustomerProfileRequestValidator
    : AbstractValidator<UpdateCustomerProfileRequest>
{
    public UpdateCustomerProfileRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .Must(name => name.Trim().Length >= 2)
            .WithMessage("Name must contain at least 2 characters.")
            .Must(name => name.Trim().Length <= 100)
            .WithMessage("Name must contain at most 100 characters.");
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class EmployeeLoginRequestValidator
    : AbstractValidator<EmployeeLoginRequest>
{
    public EmployeeLoginRequestValidator()
    {
        RuleFor(request => request.Username)
            .NotEmpty()
            .MaximumLength(64);
        RuleFor(request => request.Password).NotEmpty();
    }
}

public sealed class ChangeEmployeePasswordRequestValidator
    : AbstractValidator<ChangeEmployeePasswordRequest>
{
    public ChangeEmployeePasswordRequestValidator(
        IPasswordPolicyValidator passwordPolicyValidator)
    {
        RuleFor(request => request.CurrentPassword).NotEmpty();
        RuleFor(request => request.NewPassword)
            .NotEmpty()
            .Custom((password, context) =>
            {
                foreach (var error in passwordPolicyValidator.Validate(password))
                {
                    context.AddFailure(error);
                }
            });
    }
}
