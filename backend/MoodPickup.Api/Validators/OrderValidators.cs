using FluentValidation;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;

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

public sealed class OrderListQueryValidator : PaginationValidator<OrderListQuery>
{
    public OrderListQueryValidator()
    {
        RuleFor(query => query.Filter).IsInEnum();
        RuleFor(query => query.Search).MaximumLength(120);
    }
}

public sealed class StaffOrderListQueryValidator
    : PaginationValidator<StaffOrderListQuery>;

public sealed class ConfirmOrderRequestValidator
    : AbstractValidator<ConfirmOrderRequest>
{
    public ConfirmOrderRequestValidator()
    {
        RuleFor(request => request.EstimatedReadyTime).NotEmpty();
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class RejectOrderRequestValidator
    : AbstractValidator<RejectOrderRequest>
{
    public RejectOrderRequestValidator()
    {
        RuleFor(request => request.Reason).NotEmpty().MaximumLength(500);
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class UpdateEstimatedReadyTimeRequestValidator
    : AbstractValidator<UpdateEstimatedReadyTimeRequest>
{
    public UpdateEstimatedReadyTimeRequestValidator()
    {
        RuleFor(request => request.EstimatedReadyTime).NotEmpty();
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class KitchenOrderListQueryValidator
    : PaginationValidator<KitchenOrderListQuery>
{
    public KitchenOrderListQueryValidator()
    {
        RuleFor(query => query.Status)
            .Must(status => status is
                OrderStatus.Confirmed or
                OrderStatus.Preparing or
                OrderStatus.ReadyForPickup)
            .When(query => query.Status is not null)
            .WithMessage("Kitchen status must be Confirmed, Preparing, or ReadyForPickup.");
        RuleFor(query => query.OrderNumber).MaximumLength(32);
        RuleFor(query => query)
            .Must(query => query.CreatedFrom is null ||
                           query.CreatedTo is null ||
                           query.CreatedFrom < query.CreatedTo)
            .WithMessage("CreatedTo must be later than CreatedFrom.");
        RuleFor(query => query)
            .Must(query => query.PickupFrom is null ||
                           query.PickupTo is null ||
                           query.PickupFrom < query.PickupTo)
            .WithMessage("PickupTo must be later than PickupFrom.");
    }
}

public sealed class OrderVersionRequestValidator
    : AbstractValidator<OrderVersionRequest>
{
    public OrderVersionRequestValidator()
    {
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}

public sealed class RecordPaymentRequestValidator
    : AbstractValidator<RecordPaymentRequest>
{
    public RecordPaymentRequestValidator()
    {
        RuleFor(request => request.PaymentMethodUsed)
            .NotNull()
            .IsInEnum();
        RuleFor(request => request.RowVersion).NotEmpty();
    }
}
