using FluentValidation;
using MoodPickup.Api.DTOs.Payments;

namespace MoodPickup.Api.Validators;

public sealed class AlifCallbackRequestValidator
    : AbstractValidator<AlifCallbackRequest>
{
    public AlifCallbackRequestValidator()
    {
        RuleFor(request => request.OrderId)
            .NotEmpty()
            .MaximumLength(64)
            .Matches("^[A-Za-z0-9]+$");
        RuleFor(request => request.TransactionId)
            .NotEmpty()
            .MaximumLength(128)
            .Matches("^[A-Za-z0-9]+$");
        RuleFor(request => request.Status)
            .NotEmpty()
            .MaximumLength(64);
        RuleFor(request => request.Token)
            .NotEmpty()
            .Matches("^[A-Fa-f0-9]{64}$");
        RuleFor(request => request.Amount).GreaterThan(0);
        RuleFor(request => request.Account).NotEmpty().MaximumLength(128);
        RuleFor(request => request.TransactionType).MaximumLength(64);
    }
}
