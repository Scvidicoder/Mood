using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;

namespace MoodPickup.Api.Services;

public sealed class OrderWorkflowService(
    MoodPickupDbContext dbContext,
    ICurrentUserContext currentUser,
    IEmployeeAuditService auditService,
    IOrderRealtimeNotifier realtimeNotifier,
    IOptionsMonitor<CheckoutOptions> checkoutOptions,
    TimeProvider timeProvider,
    ILogger<OrderWorkflowService> logger) : IOrderWorkflowService
{
    private static readonly OrderStatus[] KitchenStatuses =
    [
        OrderStatus.Confirmed,
        OrderStatus.Preparing,
        OrderStatus.ReadyForPickup
    ];

    public async Task<PagedResponse<KitchenOrderDto>> GetKitchenOrdersAsync(
        KitchenOrderListQuery query,
        CancellationToken cancellationToken)
    {
        if (query.Status is not null && !KitchenStatuses.Contains(query.Status.Value))
        {
            throw new ApiValidationException(new Dictionary<string, string[]>
            {
                ["status"] = ["Kitchen status must be Confirmed, Preparing, or ReadyForPickup."]
            });
        }

        var orders = dbContext.Orders
            .AsNoTracking()
            .Where(order => KitchenStatuses.Contains(order.Status));
        if (query.Status is OrderStatus status)
        {
            orders = orders.Where(order => order.Status == status);
        }
        if (query.CreatedFrom is DateTimeOffset createdFrom)
        {
            orders = orders.Where(order => order.CreatedAt >= createdFrom.ToUniversalTime());
        }
        if (query.CreatedTo is DateTimeOffset createdTo)
        {
            orders = orders.Where(order => order.CreatedAt < createdTo.ToUniversalTime());
        }
        if (query.PickupFrom is DateTimeOffset pickupFrom)
        {
            orders = orders.Where(order =>
                (order.RequestedPickupTime ?? order.EstimatedReadyAt) >=
                pickupFrom.ToUniversalTime());
        }
        if (query.PickupTo is DateTimeOffset pickupTo)
        {
            orders = orders.Where(order =>
                (order.RequestedPickupTime ?? order.EstimatedReadyAt) <
                pickupTo.ToUniversalTime());
        }
        if (!string.IsNullOrWhiteSpace(query.OrderNumber))
        {
            var orderNumber = query.OrderNumber.Trim();
            orders = orders.Where(order => EF.Functions.ILike(
                order.OrderNumber,
                $"%{orderNumber}%"));
        }

        var totalCount = await orders.CountAsync(cancellationToken);
        var page = await orders
            .OrderBy(order =>
                order.RequestedPickupTime ?? order.EstimatedReadyAt ?? order.CreatedAt)
            .ThenBy(order => order.CreatedAt)
            .ThenBy(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Include(order => order.Items)
                .ThenInclude(item => item.Options)
            .ToListAsync(cancellationToken);

        return new PagedResponse<KitchenOrderDto>(
            page.Select(ToKitchenOrder).ToArray(),
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<KitchenOrderDto> StartPreparationAsync(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status == OrderStatus.Preparing)
        {
            throw Conflict("Order preparation has already started", "ORDER_ALREADY_PREPARING");
        }
        if (order.Status != OrderStatus.Confirmed)
        {
            throw Conflict(
                "Only a confirmed order can start preparation",
                "ORDER_CANNOT_START_PREPARATION");
        }

        var employeeId = currentUser.GetRequiredEmployeeId();
        var changedAt = timeProvider.GetUtcNow();
        var oldStatus = order.Status;
        order.Status = OrderStatus.Preparing;
        order.PreparationStartedAt = changedAt;
        order.PreparationStartedByEmployeeId = employeeId;
        AddHistory(order, oldStatus, employeeId, changedAt);
        await auditService.RecordAsync(
            "OrderPreparationStarted",
            "Order",
            order.Id,
            $"Started preparation for order '{order.OrderNumber}'.",
            new { status = oldStatus, preparationStartedAt = (DateTimeOffset?)null },
            new
            {
                status = order.Status,
                preparationStartedAt = order.PreparationStartedAt,
                preparationStartedByEmployeeId = employeeId
            },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.OrderPreparingAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToKitchenOrder(order);
    }

    public async Task<KitchenOrderDto> MarkReadyAsync(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status == OrderStatus.ReadyForPickup)
        {
            throw Conflict("Order is already ready for pickup", "ORDER_ALREADY_READY");
        }
        if (order.Status != OrderStatus.Preparing)
        {
            throw Conflict(
                "An order must be preparing before it can be marked ready",
                "ORDER_CANNOT_BE_MARKED_READY");
        }

        var employeeId = currentUser.GetRequiredEmployeeId();
        var changedAt = timeProvider.GetUtcNow();
        var oldStatus = order.Status;
        order.Status = OrderStatus.ReadyForPickup;
        order.ReadyAt = changedAt;
        order.ReadyByEmployeeId = employeeId;
        AddHistory(order, oldStatus, employeeId, changedAt);
        await auditService.RecordAsync(
            "OrderReadyForPickup",
            "Order",
            order.Id,
            $"Marked order '{order.OrderNumber}' ready for pickup.",
            new { status = oldStatus, readyAt = (DateTimeOffset?)null },
            new
            {
                status = order.Status,
                readyAt = order.ReadyAt,
                readyByEmployeeId = employeeId
            },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.OrderReadyAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToKitchenOrder(order);
    }

    public async Task<KitchenOrderDto> UpdateKitchenEtaAsync(
        Guid id,
        UpdateEstimatedReadyTimeRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status is not (OrderStatus.Confirmed or OrderStatus.Preparing))
        {
            throw Conflict(
                "Kitchen can change estimated ready time only before the order is ready",
                "ORDER_ESTIMATED_TIME_CANNOT_BE_CHANGED");
        }

        var estimatedReadyAt = EstimatedReadyTimeRules.Validate(
            request.EstimatedReadyTime,
            checkoutOptions.CurrentValue,
            timeProvider.GetUtcNow());
        if (estimatedReadyAt == order.EstimatedReadyAt)
        {
            throw Conflict(
                "Estimated ready time is unchanged",
                "ESTIMATED_READY_TIME_UNCHANGED");
        }

        var previousEstimatedReadyAt = order.EstimatedReadyAt;
        order.EstimatedReadyAt = estimatedReadyAt;
        await auditService.RecordAsync(
            "EstimatedReadyTimeChanged",
            "Order",
            order.Id,
            $"Changed estimated ready time for order '{order.OrderNumber}'.",
            new { estimatedReadyAt = previousEstimatedReadyAt },
            new { estimatedReadyAt = order.EstimatedReadyAt },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.EstimatedReadyTimeChangedAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToKitchenOrder(order);
    }

    public async Task<StaffOrderDetailDto> RecordPaymentAsync(
        Guid id,
        RecordPaymentRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status != OrderStatus.ReadyForPickup)
        {
            throw Conflict(
                "Payment can be recorded only when an order is ready for pickup",
                "ORDER_PAYMENT_CANNOT_BE_RECORDED");
        }
        if (order.PaymentMethod != PaymentMethod.PayOnPickup)
        {
            throw Conflict(
                "Online orders are already paid",
                "ORDER_PAYMENT_ALREADY_RECEIVED");
        }
        if (order.PaymentReceived)
        {
            throw Conflict("Payment is already recorded", "ORDER_PAYMENT_ALREADY_RECEIVED");
        }

        var employeeId = currentUser.GetRequiredEmployeeId();
        var receivedAt = timeProvider.GetUtcNow();
        order.PaymentReceived = true;
        order.PaymentMethodUsed = request.PaymentMethodUsed!.Value;
        order.PaymentReceivedAt = receivedAt;
        order.PaymentReceivedByEmployeeId = employeeId;
        await auditService.RecordAsync(
            "OrderPaymentReceived",
            "Order",
            order.Id,
            $"Recorded pickup payment for order '{order.OrderNumber}'.",
            new
            {
                paymentReceived = false,
                paymentMethodUsed = (PaymentMethodUsed?)null
            },
            new
            {
                paymentReceived = order.PaymentReceived,
                paymentMethodUsed = order.PaymentMethodUsed,
                paymentReceivedAt = order.PaymentReceivedAt,
                paymentReceivedByEmployeeId = employeeId
            },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.PaymentStatusChangedAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToStaffDetail(order);
    }

    public async Task<StaffOrderDetailDto> CompleteAsync(
        Guid id,
        OrderVersionRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status == OrderStatus.Completed)
        {
            throw Conflict("Order is already completed", "ORDER_ALREADY_COMPLETED");
        }
        if (order.Status != OrderStatus.ReadyForPickup)
        {
            throw Conflict(
                "An order must be ready for pickup before completion",
                "ORDER_CANNOT_BE_COMPLETED");
        }
        if (order.PaymentMethod == PaymentMethod.PayOnPickup && !order.PaymentReceived)
        {
            throw Conflict(
                "Pickup payment must be recorded before completion",
                "ORDER_PAYMENT_REQUIRED");
        }
        if (order.PaymentMethod == PaymentMethod.Online &&
            order.Payment?.Status != PaymentStatus.Paid)
        {
            throw Conflict(
                "Online payment must be confirmed before completion",
                "ORDER_PAYMENT_REQUIRED");
        }

        var employeeId = currentUser.GetRequiredEmployeeId();
        var changedAt = timeProvider.GetUtcNow();
        var oldStatus = order.Status;
        order.Status = OrderStatus.Completed;
        order.CompletedAt = changedAt;
        order.CompletedByEmployeeId = employeeId;
        AddHistory(order, oldStatus, employeeId, changedAt);
        await auditService.RecordAsync(
            "OrderCompleted",
            "Order",
            order.Id,
            $"Completed order '{order.OrderNumber}'.",
            new { status = oldStatus, completedAt = (DateTimeOffset?)null },
            new
            {
                status = order.Status,
                completedAt = order.CompletedAt,
                completedByEmployeeId = employeeId,
                paymentReceived = order.PaymentReceived,
                paymentMethodUsed = order.PaymentMethodUsed
            },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.OrderCompletedAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToStaffDetail(order);
    }

    private async Task<Order> GetTrackedOrderAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .Include(order => order.Items)
                .ThenInclude(item => item.Options)
            .Include(order => order.StatusHistory)
            .Include(order => order.Payment)
            .SingleOrDefaultAsync(order => order.Id == id, cancellationToken)
            ?? throw NotFound();
    }

    private void AddHistory(
        Order order,
        OrderStatus oldStatus,
        Guid employeeId,
        DateTimeOffset timestamp)
    {
        var history = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OldStatus = oldStatus,
            NewStatus = order.Status,
            Timestamp = timestamp,
            EmployeeId = employeeId,
            CorrelationId = currentUser.CorrelationId
        };
        order.StatusHistory.Add(history);
        dbContext.OrderStatusHistories.Add(history);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            logger.LogError(
                exception,
                "Order workflow concurrency update failed for entries {Entries}.",
                string.Join(",", exception.Entries.Select(entry => entry.Metadata.ClrType.Name)));
            throw VersionConflict();
        }
    }

    private async Task NotifySafelyAsync(
        Func<Task> send,
        Order order,
        CancellationToken cancellationToken)
    {
        try
        {
            await send();
        }
        catch (Exception exception) when (!cancellationToken.IsCancellationRequested)
        {
            logger.LogError(
                exception,
                "Failed to publish order workflow update for {OrderId}.",
                order.Id);
        }
    }

    private static KitchenOrderDto ToKitchenOrder(Order order)
    {
        return new KitchenOrderDto(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerPhoneNumber,
            order.CreatedAt,
            order.PickupMode,
            order.RequestedPickupTime,
            order.EstimatedReadyAt,
            order.PreparationStartedAt,
            order.ReadyAt,
            order.Status,
            order.PaymentMethod,
            order.PaymentReceived,
            order.PaymentMethodUsed,
            order.Total,
            order.Currency,
            order.Comment,
            order.Items.Sum(item => item.Quantity),
            order.RowVersion,
            OrderDtoMapper.ToItems(order));
    }

    private static StaffOrderDetailDto ToStaffDetail(Order order)
    {
        return new StaffOrderDetailDto(
            order.Id,
            order.OrderNumber,
            order.CustomerName,
            order.CustomerPhoneNumber,
            order.Status,
            order.PaymentMethod,
            order.PickupMode,
            order.RequestedPickupTime,
            order.EstimatedReadyAt,
            order.Comment,
            order.RejectReason,
            order.Subtotal,
            order.DiscountTotal,
            order.Total,
            order.Currency,
            order.CreatedAt,
            order.ConfirmedAt,
            order.RejectedAt,
            order.PreparationStartedAt,
            order.ReadyAt,
            order.CompletedAt,
            order.PaymentReceived,
            order.PaymentMethodUsed,
            order.RowVersion,
            OrderDtoMapper.ToStatusHistory(order),
            OrderDtoMapper.ToItems(order),
            order.Payment is null ? null : OrderDtoMapper.ToStaffPayment(order.Payment));
    }

    private OrderRealtimeEventDto ToRealtimeEvent(Order order)
    {
        return new OrderRealtimeEventDto(
            Guid.NewGuid(),
            timeProvider.GetUtcNow(),
            order.Id,
            order.OrderNumber,
            order.Status,
            order.EstimatedReadyAt,
            order.RejectReason,
            order.PreparationStartedAt,
            order.ReadyAt,
            order.CompletedAt,
            order.PaymentReceived,
            order.PaymentMethodUsed,
            order.Payment?.Id,
            order.Payment?.Status,
            order.Payment?.PaidAt,
            order.Payment?.RefundedAt);
    }

    private static void EnsureVersion(Guid expected, Order order)
    {
        if (expected != order.RowVersion)
        {
            throw VersionConflict(order);
        }
    }

    private static ApiProblemException VersionConflict(Order? order = null)
    {
        return Conflict(
            "The order was changed by another employee",
            "ORDER_VERSION_CONFLICT",
            "Refresh the order and try again.",
            "concurrency_conflict",
            order is null
                ? null
                : new Dictionary<string, object?>
                {
                    ["currentResource"] = new
                    {
                        id = order.Id,
                        rowVersion = order.RowVersion
                    }
                });
    }

    private static ApiProblemException NotFound()
    {
        return new ApiProblemException(
            StatusCodes.Status404NotFound,
            "not_found",
            "Order not found",
            "ORDER_NOT_FOUND");
    }

    private static ApiProblemException Conflict(
        string title,
        string code,
        string? detail = null,
        string type = "business_rule_violation",
        IReadOnlyDictionary<string, object?>? extensions = null)
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            type,
            title,
            code,
            detail,
            extensions);
    }
}
