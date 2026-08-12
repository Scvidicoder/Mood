using System.Data;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.DTOs.Payments;
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
    IPaymentService paymentService,
    IOptionsMonitor<CheckoutOptions> checkoutOptions,
    TimeProvider timeProvider) : IOrderService
{
    private const decimal MaximumStoredAmount = 9_999_999_999.99m;

    public Task<PickupSlotsDto> GetPickupSlotsAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var options = checkoutOptions.CurrentValue;
        var now = timeProvider.GetUtcNow();
        return Task.FromResult(CreatePickupSlots(options, now));
    }

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
                PaymentReceived = false
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
                        OptionGroupId = option.ProductOptionGroup.OptionGroupId,
                        OptionValueId = option.OptionValueId,
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
            PaymentLaunchResponse? paymentLaunch = null;
            if (order.PaymentMethod == PaymentMethod.Online)
            {
                paymentLaunch = await paymentService.CreateForOrderAsync(
                    order,
                    cancellationToken);
            }

            await dbContext.SaveChangesAsync(cancellationToken);

            if (transaction is not null)
            {
                await transaction.CommitAsync(cancellationToken);
            }

            return OrderDtoMapper.ToCustomerDetail(order, paymentLaunch);
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
            .Include(item => item.Payment)
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

        orders = query.Filter switch
        {
            CustomerOrderFilter.Active => orders.Where(order =>
                order.Status == OrderStatus.PendingConfirmation ||
                order.Status == OrderStatus.Confirmed ||
                order.Status == OrderStatus.Preparing ||
                order.Status == OrderStatus.ReadyForPickup),
            CustomerOrderFilter.Completed => orders.Where(order =>
                order.Status == OrderStatus.Completed),
            CustomerOrderFilter.Cancelled => orders.Where(order =>
                order.Status == OrderStatus.Cancelled),
            CustomerOrderFilter.Rejected => orders.Where(order =>
                order.Status == OrderStatus.Rejected),
            _ => orders
        };

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var normalizedSearch = query.Search.Trim().ToLowerInvariant();
            orders = orders.Where(order =>
                order.OrderNumber.ToLower().Contains(normalizedSearch) ||
                order.Items.Any(item =>
                    item.ProductName.ToLower().Contains(normalizedSearch)));
        }

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
                order.PaymentMethodUsed,
                order.Payment == null ? null : order.Payment.Status,
                order.Payment == null ? null : order.Payment.PaidAt,
                order.Payment == null ? null : order.Payment.RefundedAt))
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
            .Include(item => item.Payment)
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

    public async Task<RepeatOrderResultDto> RepeatAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var customerId = currentUser.GetRequiredCustomerId();
        var order = await dbContext.Orders
            .AsNoTracking()
            .Include(item => item.Items)
                .ThenInclude(item => item.Options)
            .SingleOrDefaultAsync(
                item => item.Id == id && item.CustomerId == customerId,
                cancellationToken)
            ?? throw new ApiProblemException(
                StatusCodes.Status404NotFound,
                "not_found",
                "Order not found",
                "ORDER_NOT_FOUND");

        var productIds = order.Items.Select(item => item.ProductId).Distinct().ToArray();
        var products = await dbContext.Products
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(product => product.Category)
            .Include(product => product.OptionGroups)
                .ThenInclude(group => group.OptionGroup)
            .Include(product => product.OptionGroups)
                .ThenInclude(group => group.Values)
                    .ThenInclude(value => value.OptionValue)
            .Where(product => productIds.Contains(product.Id))
            .ToDictionaryAsync(product => product.Id, cancellationToken);

        var availableItems = new List<RepeatOrderItemDto>();
        var unavailableItems = new List<RepeatOrderIssueDto>();
        var currency = checkoutOptions.CurrentValue.Currency.ToUpperInvariant();

        foreach (var historicalItem in order.Items.OrderBy(item => item.Id))
        {
            var reasons = new List<string>();
            if (!products.TryGetValue(historicalItem.ProductId, out var product))
            {
                reasons.Add("This product no longer exists.");
                unavailableItems.Add(ToRepeatIssue(historicalItem, reasons));
                continue;
            }

            if (product.IsDeleted ||
                !product.IsVisible ||
                !product.IsAvailable ||
                product.Category.IsDeleted ||
                !product.Category.IsVisible)
            {
                reasons.Add("This product is not currently available to order.");
            }

            var orderability = menuConfigurationValidator.EvaluateOrderability(product);
            reasons.AddRange(orderability.Issues.Select(issue => issue.Message));

            var activeGroups = product.OptionGroups
                .Where(group =>
                    group.IsActive &&
                    group.OptionGroup.IsActive &&
                    !group.OptionGroup.IsDeleted)
                .ToArray();
            var selectedOptions = new List<ProductOptionValue>();

            foreach (var historicalOption in historicalItem.Options
                         .OrderBy(option => option.DisplayOrder)
                         .ThenBy(option => option.OptionGroupName))
            {
                var matches = activeGroups
                    .SelectMany(group => group.Values)
                    .Where(value => MatchesHistoricalOption(value, historicalOption))
                    .ToArray();

                if (matches.Length != 1)
                {
                    reasons.Add(
                        $"{historicalOption.OptionGroupName}: {historicalOption.OptionValueName} no longer exists for this product.");
                    continue;
                }

                var currentOption = matches[0];
                if (!IsAvailableForCheckout(currentOption))
                {
                    reasons.Add(
                        $"{historicalOption.OptionGroupName}: {historicalOption.OptionValueName} is currently unavailable.");
                    continue;
                }

                selectedOptions.Add(currentOption);
            }

            foreach (var group in activeGroups)
            {
                var selectedCount = selectedOptions.Count(option =>
                    option.ProductOptionGroupId == group.Id);
                if (selectedCount < group.MinimumSelections ||
                    selectedCount > group.MaximumSelections ||
                    group.OptionGroup.SelectionType == OptionSelectionType.Single &&
                    selectedCount > 1)
                {
                    reasons.Add(
                        $"The current selection requirements for {group.OptionGroup.Name} are not satisfied.");
                }
            }

            var distinctReasons = reasons.Distinct(StringComparer.Ordinal).ToArray();
            if (distinctReasons.Length > 0)
            {
                unavailableItems.Add(ToRepeatIssue(historicalItem, distinctReasons));
                continue;
            }

            availableItems.Add(new RepeatOrderItemDto(
                product.Id,
                product.Name,
                product.BasePrice,
                product.BasePrice + selectedOptions.Sum(option => option.PriceModifier),
                currency,
                historicalItem.Quantity,
                selectedOptions
                    .OrderBy(option => option.ProductOptionGroup.DisplayOrder)
                    .ThenBy(option => option.DisplayOrder)
                    .Select(option => new RepeatOrderOptionDto(
                        option.ProductOptionGroup.Id,
                        option.ProductOptionGroup.OptionGroup.Name,
                        option.OptionValueId,
                        option.OptionValue.Name,
                        option.PriceModifier,
                        option.VolumeMilliliters,
                        option.Calories))
                    .ToArray()));
        }

        return new RepeatOrderResultDto(
            order.OrderNumber,
            availableItems,
            unavailableItems);
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

        var lastPickup = closing.AddMinutes(-30);
        if (requestedTime < opening || requestedTime > lastPickup)
        {
            AddPickupError(
                errors,
                $"Scheduled pickup must be between {options.OpeningTime} and {lastPickup:HH:mm} today.");
        }

        var requestedUtc = request.RequestedPickupTime.Value.ToUniversalTime();
        if (requestedUtc <= now)
        {
            AddPickupError(errors, "Choose a future pickup time from the available times.");
        }

        return errors;
    }

    private static PickupSlotsDto CreatePickupSlots(
        CheckoutOptions options,
        DateTimeOffset now)
    {
        var timeZone = GetTimeZone(options.TimeZoneId);
        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var opening = TimeOnly.ParseExact(
            options.OpeningTime,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var closing = TimeOnly.ParseExact(
            options.ClosingTime,
            "HH:mm",
            CultureInfo.InvariantCulture);
        var lastPickup = closing.AddMinutes(-30);
        var currentMinute = localNow.Hour * 60 + localNow.Minute;
        var nextIntervalMinute =
            (currentMinute / options.PickupIntervalMinutes + 1) *
            options.PickupIntervalMinutes;
        var openingMinute = opening.Hour * 60 + opening.Minute;
        var lastPickupMinute = lastPickup.Hour * 60 + lastPickup.Minute;
        var firstMinute = Math.Max(openingMinute, nextIntervalMinute);
        var slots = new List<PickupSlotDto>();

        for (
            var minute = firstMinute;
            minute <= lastPickupMinute;
            minute += options.PickupIntervalMinutes)
        {
            var localDateTime = DateTime.SpecifyKind(
                localNow.Date.AddMinutes(minute),
                DateTimeKind.Unspecified);
            var startsAt = new DateTimeOffset(
                localDateTime,
                timeZone.GetUtcOffset(localDateTime));
            slots.Add(new PickupSlotDto(startsAt.ToString("HH:mm"), startsAt));
        }

        return new PickupSlotsDto(
            true,
            DateOnly.FromDateTime(localNow.Date),
            options.PickupIntervalMinutes,
            slots);
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

    private static bool MatchesHistoricalOption(
        ProductOptionValue current,
        OrderItemOption historical)
    {
        if (historical.OptionValueId is Guid optionValueId)
        {
            return current.OptionValueId == optionValueId &&
                   (historical.OptionGroupId is null ||
                    current.ProductOptionGroup.OptionGroupId == historical.OptionGroupId);
        }

        return string.Equals(
                   current.ProductOptionGroup.OptionGroup.Name,
                   historical.OptionGroupName,
                   StringComparison.OrdinalIgnoreCase) &&
               string.Equals(
                   current.OptionValue.Name,
                   historical.OptionValueName,
                   StringComparison.OrdinalIgnoreCase);
    }

    private static RepeatOrderIssueDto ToRepeatIssue(
        OrderItem item,
        IReadOnlyList<string> reasons)
    {
        return new RepeatOrderIssueDto(
            item.ProductName,
            item.Quantity,
            reasons);
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
