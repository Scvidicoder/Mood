using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Orders;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class OrderServiceTests
{
    [Fact]
    public async Task Checkout_CreatesAnImmutableServerCalculatedSnapshot()
    {
        await using var fixture = await OrderFixture.CreateAsync();
        var service = fixture.CreateService();

        var created = await service.CreateAsync(
            new CreateOrderRequest(
                [new CreateOrderItemRequest(
                    fixture.Product.Id,
                    [fixture.OptionValue.Id],
                    2,
                    "Less foam")],
                "Please keep it warm",
                PaymentMethod.PayOnPickup,
                PickupMode.AsSoonAsPossible,
                null),
            CancellationToken.None);

        Assert.Equal(OrderStatus.PendingConfirmation, created.Status);
        Assert.Equal("MP-", created.OrderNumber[..3]);
        Assert.Equal(48m, created.Subtotal);
        var item = Assert.Single(created.Items);
        Assert.Equal("Cappuccino", item.ProductName);
        Assert.True(item.IsAvailableAtPurchase);
        Assert.Equal(22m, item.BasePrice);
        Assert.Equal(24m, item.FinalPrice);
        Assert.Equal(2, item.Quantity);
        var option = Assert.Single(item.Options);
        Assert.Equal("Size", option.OptionGroupName);
        Assert.Equal("Small", option.OptionValueName);
        Assert.Equal(2m, option.PriceModifier);

        fixture.Product.Name = "Renamed Cappuccino";
        fixture.Product.BasePrice = 90m;
        fixture.ProductOption.PriceModifier = 15m;
        await fixture.DbContext.SaveChangesAsync();

        var persisted = await fixture.DbContext.Orders
            .AsNoTracking()
            .Include(order => order.Items)
                .ThenInclude(orderItem => orderItem.Options)
            .SingleAsync();
        var persistedItem = Assert.Single(persisted.Items);
        Assert.Equal("Cappuccino", persistedItem.ProductName);
        Assert.True(persistedItem.IsAvailableAtPurchase);
        Assert.Equal(22m, persistedItem.BasePrice);
        Assert.Equal(24m, persistedItem.FinalPrice);
        Assert.Equal(2m, Assert.Single(persistedItem.Options).PriceModifier);
    }

    [Fact]
    public async Task Checkout_RejectsMissingRequiredOptionsWithoutCreatingAnOrder()
    {
        await using var fixture = await OrderFixture.CreateAsync();
        var service = fixture.CreateService();

        var exception = await Assert.ThrowsAsync<ApiValidationException>(() =>
            service.CreateAsync(
                new CreateOrderRequest(
                    [new CreateOrderItemRequest(
                        fixture.Product.Id,
                        [],
                        1,
                        null)],
                    null,
                    PaymentMethod.PayOnPickup,
                    PickupMode.AsSoonAsPossible,
                    null),
                CancellationToken.None));

        Assert.Contains("items[0]", exception.Errors.Keys);
        Assert.Equal(0, await fixture.DbContext.Orders.CountAsync());
        Assert.Equal(0, await fixture.DbContext.OrderItems.CountAsync());
    }

    [Fact]
    public async Task Repeat_UsesStableSnapshotIdentifiersAndCurrentPrices()
    {
        await using var fixture = await OrderFixture.CreateAsync();
        var service = fixture.CreateService();
        var created = await service.CreateAsync(
            new CreateOrderRequest(
                [new CreateOrderItemRequest(
                    fixture.Product.Id,
                    [fixture.OptionValue.Id],
                    2,
                    null)],
                null,
                PaymentMethod.PayOnPickup,
                PickupMode.AsSoonAsPossible,
                null),
            CancellationToken.None);
        fixture.Product.BasePrice = 25m;
        fixture.ProductOption.PriceModifier = 3m;
        await fixture.DbContext.SaveChangesAsync();

        var repeated = await service.RepeatAsync(
            created.Id,
            CancellationToken.None);

        Assert.Empty(repeated.UnavailableItems);
        var item = Assert.Single(repeated.AvailableItems);
        Assert.Equal(28m, item.UnitPrice);
        Assert.Equal(2, item.Quantity);
        Assert.Equal(fixture.OptionValue.Id, Assert.Single(item.Options).OptionValueId);
    }

    [Fact]
    public async Task Repeat_ReportsUnavailableOptionWithoutSubstitution()
    {
        await using var fixture = await OrderFixture.CreateAsync();
        var service = fixture.CreateService();
        var created = await service.CreateAsync(
            new CreateOrderRequest(
                [new CreateOrderItemRequest(
                    fixture.Product.Id,
                    [fixture.OptionValue.Id],
                    1,
                    null)],
                null,
                PaymentMethod.PayOnPickup,
                PickupMode.AsSoonAsPossible,
                null),
            CancellationToken.None);
        fixture.ProductOption.IsAvailable = false;
        await fixture.DbContext.SaveChangesAsync();

        var repeated = await service.RepeatAsync(
            created.Id,
            CancellationToken.None);

        Assert.Empty(repeated.AvailableItems);
        var unavailable = Assert.Single(repeated.UnavailableItems);
        Assert.Contains(
            unavailable.Reasons,
            reason => reason.Contains("unavailable", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class OrderFixture : IAsyncDisposable
    {
        private readonly DbContextOptions<MoodPickupDbContext> _options;

        private OrderFixture(
            DbContextOptions<MoodPickupDbContext> options,
            MoodPickupDbContext dbContext,
            Customer customer,
            Product product,
            OptionValue optionValue,
            ProductOptionValue productOption)
        {
            _options = options;
            DbContext = dbContext;
            Customer = customer;
            Product = product;
            OptionValue = optionValue;
            ProductOption = productOption;
        }

        public MoodPickupDbContext DbContext { get; }

        public Customer Customer { get; }

        public Product Product { get; }

        public OptionValue OptionValue { get; }

        public ProductOptionValue ProductOption { get; }

        public static async Task<OrderFixture> CreateAsync()
        {
            var options = new DbContextOptionsBuilder<MoodPickupDbContext>()
                .UseInMemoryDatabase($"order-service-{Guid.NewGuid():N}")
                .Options;
            var dbContext = new MoodPickupDbContext(options);
            var customer = new Customer
            {
                Id = Guid.NewGuid(),
                Name = "Amina",
                PhoneNumber = "+992900000001"
            };
            var category = new Category
            {
                Id = Guid.NewGuid(),
                Name = "Coffee",
                DisplayOrder = 1,
                IsVisible = true
            };
            var group = new OptionGroup
            {
                Id = Guid.NewGuid(),
                Name = "Size",
                SelectionType = OptionSelectionType.Single,
                DefaultIsRequired = true,
                DefaultMinimumSelections = 1,
                DefaultMaximumSelections = 1,
                DisplayOrder = 1,
                IsActive = true
            };
            var optionValue = new OptionValue
            {
                Id = Guid.NewGuid(),
                OptionGroupId = group.Id,
                OptionGroup = group,
                Name = "Small",
                DisplayOrder = 1,
                IsActive = true
            };
            group.Values.Add(optionValue);
            var product = new Product
            {
                Id = Guid.NewGuid(),
                CategoryId = category.Id,
                Category = category,
                Name = "Cappuccino",
                BasePrice = 22m,
                DefaultCalories = 120,
                DefaultVolumeMilliliters = 250,
                DisplayOrder = 1,
                IsAvailable = true,
                IsVisible = true
            };
            category.Products.Add(product);
            var assignment = new ProductOptionGroup
            {
                Id = Guid.NewGuid(),
                ProductId = product.Id,
                Product = product,
                OptionGroupId = group.Id,
                OptionGroup = group,
                IsRequired = true,
                MinimumSelections = 1,
                MaximumSelections = 1,
                DisplayOrder = 1,
                IsActive = true
            };
            var productOption = new ProductOptionValue
            {
                Id = Guid.NewGuid(),
                ProductOptionGroupId = assignment.Id,
                ProductOptionGroup = assignment,
                OptionValueId = optionValue.Id,
                OptionValue = optionValue,
                PriceModifier = 2m,
                IsDefault = true,
                IsAvailable = true,
                DisplayOrder = 1,
                Calories = 100,
                VolumeMilliliters = 200
            };
            assignment.Values.Add(productOption);
            product.OptionGroups.Add(assignment);

            dbContext.AddRange(customer, category, group);
            await dbContext.SaveChangesAsync();
            return new OrderFixture(
                options,
                dbContext,
                customer,
                product,
                optionValue,
                productOption);
        }

        public OrderService CreateService()
        {
            return new OrderService(
                DbContext,
                new TestCurrentUserContext(Customer.Id),
                new MenuConfigurationValidator(),
                new StaticOptionsMonitor<CheckoutOptions>(new CheckoutOptions()),
                TimeProvider.System);
        }

        public async ValueTask DisposeAsync()
        {
            await DbContext.DisposeAsync();
            await using var cleanup = new MoodPickupDbContext(_options);
            await cleanup.Database.EnsureDeletedAsync();
        }
    }

    private sealed class TestCurrentUserContext(Guid customerId) : ICurrentUserContext
    {
        public string CorrelationId => "test";

        public Guid GetRequiredCustomerId() => customerId;

        public Guid GetRequiredEmployeeId() => throw new NotSupportedException();
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
        where T : class
    {
        public T CurrentValue => value;

        public T Get(string? name) => value;

        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
