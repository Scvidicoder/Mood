using System.Globalization;
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

public sealed class StaffOrderService(
    MoodPickupDbContext dbContext,
    ICurrentUserContext currentUser,
    IEmployeeAuditService auditService,
    IOrderRealtimeNotifier realtimeNotifier,
    IOptionsMonitor<CheckoutOptions> checkoutOptions,
    TimeProvider timeProvider,
    ILogger<StaffOrderService> logger) : IStaffOrderService
{
    public Task<PagedResponse<StaffOrderSummaryDto>> GetAsync(
        StaffOrderListQuery query,
        CancellationToken cancellationToken)
    {
        var orders = dbContext.Orders.AsNoTracking();
        if (query.Status is not null)
        {
            orders = orders.Where(order => order.Status == query.Status);
        }

        return GetPageAsync(orders, query, cancellationToken);
    }

    public async Task<StaffOrderDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Options)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound();

        return ToStaffDetail(order);
    }

    public Task<PagedResponse<StaffOrderSummaryDto>> GetKitchenOrdersAsync(
        PaginationQuery query,
        CancellationToken cancellationToken)
    {
        var confirmedOrders = dbContext.Orders
            .AsNoTracking()
            .Where(order => order.Status == OrderStatus.Confirmed);
        return GetPageAsync(confirmedOrders, query, cancellationToken);
    }

    public async Task<StaffOrderDetailDto> ConfirmAsync(
        Guid id,
        ConfirmOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status == OrderStatus.Confirmed)
        {
            throw Conflict("Order is already confirmed", "ORDER_ALREADY_CONFIRMED");
        }

        if (order.Status != OrderStatus.PendingConfirmation)
        {
            throw Conflict(
                "This order can no longer be confirmed",
                "ORDER_CANNOT_BE_CONFIRMED");
        }

        var estimatedReadyAt = ValidateEstimatedReadyTime(request.EstimatedReadyTime);
        var employeeId = currentUser.GetRequiredEmployeeId();
        var confirmedAt = timeProvider.GetUtcNow();
        var oldValues = new
        {
            status = order.Status,
            estimatedReadyAt = order.EstimatedReadyAt
        };

        order.Status = OrderStatus.Confirmed;
        order.EstimatedReadyAt = estimatedReadyAt;
        order.ConfirmedAt = confirmedAt;
        order.ConfirmedByEmployeeId = employeeId;
        await auditService.RecordAsync(
            "OrderConfirmed",
            "Order",
            order.Id,
            $"Confirmed order '{order.OrderNumber}'.",
            oldValues,
            new
            {
                status = order.Status,
                estimatedReadyAt = order.EstimatedReadyAt,
                confirmedAt = order.ConfirmedAt,
                confirmedByEmployeeId = employeeId
            },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.OrderConfirmedAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToStaffDetail(order);
    }

    public async Task<StaffOrderDetailDto> RejectAsync(
        Guid id,
        RejectOrderRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status == OrderStatus.Confirmed)
        {
            throw Conflict(
                "A confirmed order cannot be rejected",
                "CONFIRMED_ORDER_CANNOT_BE_REJECTED");
        }

        if (order.Status == OrderStatus.Rejected)
        {
            throw Conflict("Order is already rejected", "ORDER_ALREADY_REJECTED");
        }

        if (order.Status != OrderStatus.PendingConfirmation)
        {
            throw Conflict(
                "This order can no longer be rejected",
                "ORDER_CANNOT_BE_REJECTED");
        }

        var employeeId = currentUser.GetRequiredEmployeeId();
        var reason = request.Reason.Trim();
        var rejectedAt = timeProvider.GetUtcNow();
        var oldValues = new
        {
            status = order.Status,
            rejectReason = order.RejectReason
        };

        order.Status = OrderStatus.Rejected;
        order.RejectReason = reason;
        order.RejectedAt = rejectedAt;
        order.RejectedByEmployeeId = employeeId;
        await auditService.RecordAsync(
            "OrderRejected",
            "Order",
            order.Id,
            $"Rejected order '{order.OrderNumber}'.",
            oldValues,
            new
            {
                status = order.Status,
                rejectReason = order.RejectReason,
                rejectedAt = order.RejectedAt,
                rejectedByEmployeeId = employeeId
            },
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        await NotifySafelyAsync(
            () => realtimeNotifier.OrderRejectedAsync(
                order.CustomerId,
                ToRealtimeEvent(order),
                cancellationToken),
            order,
            cancellationToken);

        return ToStaffDetail(order);
    }

    public async Task<StaffOrderDetailDto> UpdateEstimatedReadyTimeAsync(
        Guid id,
        UpdateEstimatedReadyTimeRequest request,
        CancellationToken cancellationToken)
    {
        var order = await GetTrackedOrderAsync(id, cancellationToken);
        EnsureVersion(request.RowVersion, order);
        if (order.Status != OrderStatus.Confirmed)
        {
            throw Conflict(
                "Estimated ready time can be changed only for a confirmed order",
                "ORDER_ESTIMATED_TIME_CANNOT_BE_CHANGED");
        }

        var estimatedReadyAt = ValidateEstimatedReadyTime(request.EstimatedReadyTime);
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

        return ToStaffDetail(order);
    }

    private async Task<PagedResponse<StaffOrderSummaryDto>> GetPageAsync(
        IQueryable<Order> orders,
        PaginationQuery query,
        CancellationToken cancellationToken)
    {
        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => new StaffOrderSummaryDto(
                order.Id,
                order.OrderNumber,
                order.CustomerName,
                order.CustomerPhoneNumber,
                order.CreatedAt,
                order.PickupMode,
                order.RequestedPickupTime,
                order.PaymentMethod,
                order.Total,
                order.Currency,
                order.Comment,
                order.Status,
                order.EstimatedReadyAt,
                order.Items.Sum(item => item.Quantity),
                order.RowVersion))
            .ToListAsync(cancellationToken);

        return new PagedResponse<StaffOrderSummaryDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    private async Task<Order> GetTrackedOrderAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.Orders
            .Include(item => item.Items)
                .ThenInclude(item => item.Options)
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            ?? throw NotFound();
    }

    private DateTimeOffset ValidateEstimatedReadyTime(DateTimeOffset value)
    {
        var options = checkoutOptions.CurrentValue;
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.TimeZoneId);
        var now = timeProvider.GetUtcNow();
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var localValue = TimeZoneInfo.ConvertTime(value, timeZone);
        var opening = TimeOnly.ParseExact(
            options.OpeningTime,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var closing = TimeOnly.ParseExact(
            options.ClosingTime,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var localTime = TimeOnly.FromDateTime(localValue.DateTime);
        var errors = new List<string>();

        if (localValue.Date != localNow.Date)
        {
            errors.Add("Estimated ready time must be today in the cafe time zone.");
        }

        if (localTime < opening || localTime >= closing)
        {
            errors.Add(
                $"Estimated ready time must be within business hours ({options.OpeningTime}-{options.ClosingTime}).");
        }

        if (value.ToUniversalTime() <= now)
        {
            errors.Add("Estimated ready time must be after the current time.");
        }

        if (errors.Count > 0)
        {
            throw new ApiValidationException(new Dictionary<string, string[]>
            {
                ["estimatedReadyTime"] = errors.ToArray()
            });
        }

        return value.ToUniversalTime();
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw Conflict(
                "The order was changed by another employee",
                "ORDER_VERSION_CONFLICT",
                "Refresh the order and try again.",
                "concurrency_conflict");
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
                "Failed to publish customer order update for {OrderId}.",
                order.Id);
        }
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
            order.RowVersion,
            OrderDtoMapper.ToItems(order));
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
            order.RejectReason);
    }

    private static void EnsureVersion(Guid expected, Order order)
    {
        if (expected == order.RowVersion)
        {
            return;
        }

        throw Conflict(
            "The order was changed by another employee",
            "ORDER_VERSION_CONFLICT",
            "Refresh the order and try again.",
            "concurrency_conflict",
            new Dictionary<string, object?>
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
