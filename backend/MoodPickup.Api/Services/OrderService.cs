using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using Npgsql;

namespace MoodPickup.Api.Services;

public sealed class OrderService(
    MoodPickupDbContext dbContext,
    ICurrentUserContext currentUser,
    IMenuConfigurationValidator menuConfigurationValidator,
    IOptionsMonitor<CheckoutOptions> checkoutOptions,
    TimeProvider timeProvider) : IOrderService
{
    private const decimal MaximumStoredAmount = 9_999_999_999.99m;

    public async Task<OrderDetailDto> CreateAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        IDbContextTransaction? transaction = null;

        if (dbContext.Database.IsRelational())
        {
            transaction = await dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);
        }

        try
        {
            var customer = await dbContext.Customers.SingleOrDefaultAsync(
                item => item.Id == customerId,
                cancellationToken)
                ?? throw new ApiProblemException(
                    StatusCodes.Status401Unauthorized,
                    "unauthorized",
                    "Authentication required");

            var options = checkoutOptions.CurrentValue;
            var now = timeProvider.GetUtcNow();
            var localNow = TimeZoneInfo.ConvertTime(
                now,
                GetTimeZone(options.TimeZoneId));
            var pickupErrors = ValidatePickup(request, options, now, localNow);
            var validatedItems = await ValidateItemsAsync(request, cancellationToken);

            if (pickupErrors.Count > 0)
            {
                throw new ApiValidationException(pickupErrors);
            }

            var orderDate = DateOnly.FromDateTime(localNow.Date);
            var sequence = await GetNextOrderSequenceAsync(orderDate, cancellationToken);
            var subtotal = validatedItems.Sum(item => item.FinalPrice * item.Request.Quantity);
            if (subtotal > MaximumStoredAmount)
            {
                throw new ApiValidationException(new Dictionary<string, string[]>
                {
                    ["items"] = ["The order total exceeds the supported amount."]
                });
            }

            var order = new Order
            {
                Id = Guid.NewGuid(),
                CustomerId = customer.Id,
                OrderNumber = $"MP-{orderDate:yyyyMMdd}-{sequence:D5}",
                Status = OrderStatus.PendingConfirmation,
                PaymentMethod = request.PaymentMethod,
                PickupMode = request.PickupMode,
                RequestedPickupTime = request.PickupMode == PickupMode.Scheduled
                    ? request.RequestedPickupTime!.Value.ToUniversalTime()
                    : null,
                CustomerName = customer.Name,
                CustomerPhoneNumber = customer.PhoneNumber,
                Comment = NormalizeComment(request.Comment),
                Subtotal = subtotal,
                DiscountTotal = 0m,
                Total = subtotal,
                Currency = options.Currency.ToUpperInvariant(),
                PaymentReceived = request.PaymentMethod == PaymentMethod.Online
            };

            order.StatusHistory.Add(new OrderStatusHistory
            {
                Id = Guid.NewGuid(),
                OldStatus = null,
                NewStatus = OrderStatus.PendingConfirmation,
                Timestamp = now,
                CorrelationId = currentUser.CorrelationId
            });

            foreach (var item in validatedItems)
            {
                var orderItem = new OrderItem
                {
                    Id = Guid.NewGuid(),
                    ProductId = item.Product.Id,
                    ProductName = item.Product.Name,
                    IsAvailableAtPurchase = item.Product.IsAvailable,
                    BasePrice = item.Product.BasePrice,
                    FinalPrice = item.FinalPrice,
                    Calories = ConfiguredMetric(
                        item.Product.DefaultCalories,
                        item.SelectedOptions.Select(option => option.Calories)),
                    VolumeMilliliters = ConfiguredMetric(
                        item.Product.DefaultVolumeMilliliters,
                        item.SelectedOptions.Select(option => option.VolumeMilliliters)),
                    WeightGrams = item.Product.DefaultWeightGrams,
                    Quantity = item.Request.Quantity,
                    Comment = NormalizeComment(item.Request.Comment)
                };

                foreach (var option in item.SelectedOptions
                             .OrderBy(value => value.ProductOptionGroup.DisplayOrder)
                             .ThenBy(value => value.DisplayOrder)
                             .ThenBy(value => value.OptionValue.Name))
                {
                    orderItem.Options.Add(new OrderItemOption
                    {
                        Id = Guid.NewGuid(),
                        OptionGroupName = option.ProductOptionGroup.OptionGroup.Name,
                        OptionValueName = option.OptionValue.Name,
                        PriceModifier = option.PriceModifier,
                        CaloriesModifier = option.Calories,
                        VolumeModifier = option.VolumeMilliliters,
                        DisplayOrder = option.DisplayOrder
                    });
                }

                order.Items.Add(orderItem);
            }

            dbContext.Orders.Add(order);
            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return OrderDtoMapper.ToCustomerDetail(order);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw CheckoutConcurrencyConflict();
        }
        catch (PostgresException exception) when (
            exception.SqlState is PostgresErrorCodes.SerializationFailure or
            PostgresErrorCodes.DeadlockDetected)
        {
            throw CheckoutConcurrencyConflict();
        }
        finally
        {
            if (transaction is not null)
            {
                await transaction.DisposeAsync();
            }
        }
    }

    public async Task<OrderDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Options)
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.CustomerId == customerId,
                cancellationToken)
            ?? throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "not_found",
                "Order not found",
                "ORDER_NOT_FOUND");

        return OrderDtoMapper.ToCustomerDetail(order);
    }

    public async Task<PagedResponse<OrderSummaryDto>> GetMineAsync(
        OrderListQuery query,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var orders = dbContext.Orders
            .AsNoTracking()
            .Where(order => order.CustomerId == customerId);

        var totalCount = await orders.CountAsync(cancellationToken);
        var items = await orders
            .OrderByDescending(order => order.CreatedAt)
            .ThenByDescending(order => order.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(order => new OrderSummaryDto(
                order.Id,
                order.OrderNumber,
                order.Status,
                order.PaymentMethod,
                order.PickupMode,
                order.RequestedPickupTime,
                order.Total,
                order.Currency,
                order.Items.Sum(item => item.Quantity),
                order.CreatedAt,
                order.EstimatedReadyAt,
                order.RejectReason,
                order.PreparationStartedAt,
                order.ReadyAt,
                order.CompletedAt,
                order.PaymentReceived,
                order.PaymentMethodUsed))
            .ToListAsync(cancellationToken);

        return new PagedResponse<OrderSummaryDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<OrderDetailDto> CancelAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var order = await dbContext.Orders
            .Include(item => item.Items)
                .ThenInclude(item => item.Options)
            .Include(item => item.StatusHistory)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.CustomerId == customerId,
                cancellationToken)
            ?? throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "not_found",
                "Order not found",
                "ORDER_NOT_FOUND");

        if (order.Status != OrderStatus.PendingConfirmation)
        {
            throw new ApiProblemException(
                StatusCodes.Status409Conflict,
                "business_rule_violation",
                "Only an order awaiting confirmation can be cancelled",
                "ORDER_CANNOT_BE_CANCELLED");
        }

        var oldStatus = order.Status;
        order.Status = OrderStatus.Cancelled;
        var cancellationHistory = new OrderStatusHistory
        {
            Id = Guid.NewGuid(),
            OldStatus = oldStatus,
            NewStatus = order.Status,
            Timestamp = timeProvider.GetUtcNow(),
            CorrelationId = currentUser.CorrelationId
        };
        order.StatusHistory.Add(cancellationHistory);
        dbContext.OrderStatusHistories.Add(cancellationHistory);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw OrderConcurrencyConflict();
        }

        return OrderDtoMapper.ToCustomerDetail(order);
    }

    private async Task<IReadOnlyList<ValidatedOrderItem>> ValidateItemsAsync(
        CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var productIds = request.Items.Select(item => item.ProductId).Distinct().ToArray();
        var products = await dbContext.Products
            .IgnoreQueryFilters()
            .Include(product => product.Category)
            .Include(product => product.OptionGroups)
                .ThenInclude(group => group.OptionGroup)
            .Include(product => product.OptionGroups)
                .ThenInclude(group => group.Values)
                    .ThenInclude(value => value.OptionValue)
            .Where(product => productIds.Contains(product.Id))
            .ToListAsync(cancellationToken);
        var productsById = products.ToDictionary(product => product.Id);
        var errors = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        var validated = new List<ValidatedOrderItem>();

        for (var index = 0; index < request.Items.Count; index++)
        {
            var requestItem = request.Items[index];
            var itemErrors = new List<string>();
            if (!productsById.TryGetValue(requestItem.ProductId, out var product))
            {
                itemErrors.Add("The selected product no longer exists.");
                AddErrors(errors, index, itemErrors);
                continue;
            }

            var orderability = menuConfigurationValidator.EvaluateOrderability(product);
            if (!orderability.IsOrderable)
            {
                itemErrors.AddRange(orderability.Issues.Select(issue => issue.Message));
            }

            var requestedIds = requestItem.OptionValueIds.ToHashSet();
            var activeGroups = product.OptionGroups
                .Where(group => group.IsActive)
                .ToArray();
            var selectedOptions = new List<ProductOptionValue>();
            var compatibleOptions = activeGroups
                .SelectMany(group => group.Values)
                .Where(value => requestedIds.Contains(value.OptionValueId))
                .ToArray();

            if (compatibleOptions.Select(value => value.OptionValueId).Distinct().Count() !=
                requestedIds.Count)
            {
                itemErrors.Add("One or more selected options are not compatible with this product.");
            }

            foreach (var group in activeGroups)
            {
                var selectedForGroup = group.Values
                    .Where(value => requestedIds.Contains(value.OptionValueId))
                    .ToArray();
                var validSelectedForGroup = selectedForGroup
                    .Where(IsAvailableForCheckout)
                    .ToArray();

                if (selectedForGroup.Any(value => !IsAvailableForCheckout(value)))
                {
                    itemErrors.Add(
                        $"One or more selected values for {group.OptionGroup.Name} are unavailable.");
                }

                if (selectedForGroup.Length < group.MinimumSelections)
                {
                    itemErrors.Add(
                        $"Select at least {group.MinimumSelections} option(s) for {group.OptionGroup.Name}.");
                }

                if (selectedForGroup.Length > group.MaximumSelections)
                {
                    itemErrors.Add(
                        $"Select no more than {group.MaximumSelections} option(s) for {group.OptionGroup.Name}.");
                }

                if (group.OptionGroup.SelectionType == OptionSelectionType.Single &&
                    selectedForGroup.Length > 1)
                {
                    itemErrors.Add($"Select only one option for {group.OptionGroup.Name}.");
                }

                selectedOptions.AddRange(validSelectedForGroup);
            }

            var finalPrice = product.BasePrice + selectedOptions.Sum(value => value.PriceModifier);
            if (finalPrice < 0 || finalPrice > MaximumStoredAmount)
            {
                itemErrors.Add("The configured item price cannot be stored safely.");
            }

            if (itemErrors.Count > 0)
            {
                AddErrors(errors, index, itemErrors);
                continue;
            }

            validated.Add(new ValidatedOrderItem(requestItem, product, selectedOptions, finalPrice));
        }

        if (errors.Count > 0)
        {
            throw new ApiValidationException(errors.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Distinct(StringComparer.Ordinal).ToArray(),
                StringComparer.Ordinal));
        }

        return validated;
    }

    private static IReadOnlyDictionary<string, string[]> ValidatePickup(
        CreateOrderRequest request,
        CheckoutOptions options,
        DateTimeOffset now,
        DateTimeOffset localNow)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (request.PickupMode == PickupMode.AsSoonAsPossible)
        {
            if (request.RequestedPickupTime is not null)
            {
                errors["requestedPickupTime"] =
                    ["Requested pickup time must be empty when preparing as soon as possible."];
            }

            return errors;
        }

        if (request.RequestedPickupTime is null)
        {
            errors["requestedPickupTime"] = ["A scheduled pickup time is required."];
            return errors;
        }

        var localRequested = TimeZoneInfo.ConvertTime(
            request.RequestedPickupTime.Value,
            GetTimeZone(options.TimeZoneId));
        var requestedTime = TimeOnly.FromDateTime(localRequested.DateTime);
        var opening = TimeOnly.ParseExact(
            options.OpeningTime,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);
        var closing = TimeOnly.ParseExact(
            options.ClosingTime,
            "HH:mm",
            System.Globalization.CultureInfo.InvariantCulture);

        if (localRequested.Date != localNow.Date)
        {
            AddPickupError(errors, "Scheduled pickup is available today only.");
        }

        if (localRequested.Second != 0 ||
            localRequested.Millisecond != 0 ||
            requestedTime.Minute % options.PickupIntervalMinutes != 0)
        {
            AddPickupError(errors, "Scheduled pickup must use a 15-minute interval.");
        }

        if (requestedTime < opening || requestedTime >= closing)
        {
            AddPickupError(
                errors,
                $"Scheduled pickup must be within business hours ({options.OpeningTime}-{options.ClosingTime}).");
        }

        var requestedUtc = request.RequestedPickupTime.Value.ToUniversalTime();
        if (requestedUtc < now || requestedUtc > now.AddHours(options.SchedulingWindowHours))
        {
            AddPickupError(errors, "Scheduled pickup must be within the next 4 hours.");
        }

        return errors;
    }

    private async Task<int> GetNextOrderSequenceAsync(
        DateOnly orderDate,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            return await dbContext.Orders.CountAsync(
                order => order.CreatedAt.Date == timeProvider.GetUtcNow().Date,
                cancellationToken) + 1;
        }

        var connection = dbContext.Database.GetDbConnection();
        var openedConnection = connection.State != ConnectionState.Open;
        if (openedConnection)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.Transaction = dbContext.Database.CurrentTransaction?
                .GetDbTransaction();
            command.CommandText = """
                INSERT INTO "OrderDailySequences" ("OrderDate", "LastValue")
                VALUES (@orderDate, 1)
                ON CONFLICT ("OrderDate")
                DO UPDATE SET "LastValue" = "OrderDailySequences"."LastValue" + 1
                RETURNING "LastValue";
                """;
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@orderDate";
            parameter.DbType = DbType.Date;
            parameter.Value = orderDate;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt32(result, CultureInfo.InvariantCulture);
        }
        finally
        {
            if (openedConnection)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
        }
    }

    private static bool IsAvailableForCheckout(ProductOptionValue value)
    {
        return value.IsAvailable &&
               value.OptionValue.IsActive &&
               !value.OptionValue.IsDeleted &&
               value.OptionValue.OptionGroupId == value.ProductOptionGroup.OptionGroupId;
    }

    private static int? ConfiguredMetric(
        int? baseValue,
        IEnumerable<int?> optionValues)
    {
        var values = optionValues
            .Where(value => value is not null)
            .Select(value => value!.Value)
            .Distinct()
            .Take(2)
            .ToArray();
        return values.Length == 1 ? values[0] : baseValue;
    }

    private static void AddErrors(
        IDictionary<string, List<string>> errors,
        int itemIndex,
        IReadOnlyCollection<string> messages)
    {
        if (messages.Count > 0)
        {
            errors[$"items[{itemIndex}]"] = messages.ToList();
        }
    }

    private static void AddPickupError(
        IDictionary<string, string[]> errors,
        string message)
    {
        errors["requestedPickupTime"] = errors.TryGetValue(
            "requestedPickupTime",
            out var existing)
            ? [.. existing, message]
            : [message];
    }

    private static TimeZoneInfo GetTimeZone(string timeZoneId)
    {
        return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
    }

    private static string? NormalizeComment(string? comment)
    {
        return string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
    }

    private static ApiProblemException CheckoutConcurrencyConflict()
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "concurrency_conflict",
            "The menu changed while checkout was being processed",
            "CHECKOUT_CONCURRENCY_CONFLICT",
            "Refresh the cart and try checkout again.");
    }

    private static ApiProblemException OrderConcurrencyConflict()
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "concurrency_conflict",
            "The order was changed while the request was being processed",
            "ORDER_VERSION_CONFLICT",
            "Refresh the order and try again.");
    }

    private sealed record ValidatedOrderItem(
        CreateOrderItemRequest Request,
        Product Product,
        IReadOnlyList<ProductOptionValue> SelectedOptions,
        decimal FinalPrice);
}
