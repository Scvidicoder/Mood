using FluentValidation;
using MoodPickup.Api.DTOs.Orders;

namespace MoodPickup.Api.Validators;

public sealed class CreateOrderRequestValidator
    : AbstractValidator<CreateOrderRequest>
{
    public CreateOrderRequestValidator()
    {
        RuleFor(request => request.Items)
            .NotEmpty()
            .Must(items => items.Count <= 100)
            .WithMessage("Checkout can contain at most 100 item configurations.");
        RuleFor(request => request.Comment).MaximumLength(500);
        RuleFor(request => request.PaymentMethod).IsInEnum();
        RuleFor(request => request.PickupMode).IsInEnum();
        RuleForEach(request => request.Items).SetValidator(new CreateOrderItemRequestValidator());
    }
}

public sealed class CreateOrderItemRequestValidator
    : AbstractValidator<CreateOrderItemRequest>
{
    public CreateOrderItemRequestValidator()
    {
        RuleFor(item => item.ProductId).NotEmpty();
        RuleFor(item => item.OptionValueIds)
            .NotNull()
            .Must(ids => ids.Distinct().Count() == ids.Count)
            .WithMessage("Selected options cannot contain duplicates.");
        RuleForEach(item => item.OptionValueIds).NotEmpty();
        RuleFor(item => item.Quantity).InclusiveBetween(1, 99);
        RuleFor(item => item.Comment).MaximumLength(500);
    }
}

public sealed class OrderListQueryValidator : PaginationValidator<OrderListQuery>;
