using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class MenuDomainTests
{
    private readonly MenuConfigurationValidator _validator = new();

    [Fact]
    public async Task Category_CanBeCreatedAndNamesAreNormalized()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var category = CreateCategory("  Coffee  ", 2);
        database.Context.Categories.Add(category);

        await database.Context.SaveChangesAsync();

        var saved = await database.Context.Categories.SingleAsync();
        Assert.Equal("Coffee", saved.Name);
        Assert.Equal("coffee", saved.NormalizedName);
        Assert.Equal(2, saved.DisplayOrder);
        Assert.NotEqual(Guid.Empty, saved.RowVersion);
    }

    [Fact]
    public async Task Category_NegativeDisplayOrderIsRejected()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        database.Context.Categories.Add(CreateCategory("Coffee", -1));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Category_SoftDeleteExcludesCategoryButRetainsProduct()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var category = CreateCategory("Coffee", 0);
        var product = CreateProduct(category, "Americano", 22m);
        database.Context.AddRange(category, product);
        await database.Context.SaveChangesAsync();

        category.IsDeleted = true;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        Assert.Empty(await database.Context.Categories.ToListAsync());
        Assert.Single(await database.Context.Products.ToListAsync());
        Assert.Single(await database.Context.Categories
            .IgnoreQueryFilters()
            .Include(item => item.Products)
            .SelectMany(item => item.Products)
            .ToListAsync());
    }

    [Fact]
    public async Task Product_RequiresCategoryAndRejectsNegativePrice()
    {
        await using var missingCategoryDatabase = await RelationalMenuDatabase.CreateAsync();
        missingCategoryDatabase.Context.Products.Add(new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = Guid.NewGuid(),
            Name = "Orphan",
            BasePrice = 10m
        });

        await Assert.ThrowsAsync<DbUpdateException>(
            () => missingCategoryDatabase.Context.SaveChangesAsync());

        await using var negativePriceDatabase = await RelationalMenuDatabase.CreateAsync();
        var category = CreateCategory("Coffee", 0);
        negativePriceDatabase.Context.Products.Add(
            CreateProduct(category, "Invalid", -0.01m));

        await Assert.ThrowsAsync<DbUpdateException>(
            () => negativePriceDatabase.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task Product_MayHaveNoOptionsAndOrderingIsPreserved()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var category = CreateCategory("Desserts", 0);
        database.Context.Products.AddRange(
            CreateProduct(category, "Third", 30m, displayOrder: 3),
            CreateProduct(category, "First", 10m, displayOrder: 1),
            CreateProduct(category, "Second", 20m, displayOrder: 2));
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var products = await database.Context.Products
            .OrderBy(product => product.DisplayOrder)
            .Include(product => product.OptionGroups)
            .ToListAsync();

        Assert.Equal(["First", "Second", "Third"], products.Select(product => product.Name));
        Assert.All(products, product => Assert.Empty(product.OptionGroups));
    }

    [Fact]
    public void Product_HiddenOrUnavailableIsNotOrderable()
    {
        var hidden = CreateOrderableProduct();
        hidden.IsVisible = false;
        var unavailable = CreateOrderableProduct();
        unavailable.IsAvailable = false;

        Assert.Contains(
            _validator.EvaluateOrderability(hidden).Issues,
            issue => issue.Code == "PRODUCT_HIDDEN");
        Assert.Contains(
            _validator.EvaluateOrderability(unavailable).Issues,
            issue => issue.Code == "PRODUCT_UNAVAILABLE");
    }

    [Fact]
    public void OptionGroup_RejectsInvalidSelectionConstraints()
    {
        var single = CreateOptionGroup("Size", OptionSelectionType.Single, 0, 2);
        var invalidRange = CreateOptionGroup("Syrups", OptionSelectionType.Multiple, 3, 2);
        var required = CreateOptionGroup("Milk", OptionSelectionType.Single, 0, 1);
        required.DefaultIsRequired = true;

        Assert.Contains(
            _validator.ValidateOptionGroup(single).Issues,
            issue => issue.Code == "SINGLE_OPTION_GROUP_MAXIMUM_EXCEEDS_ONE");
        Assert.Contains(
            _validator.ValidateOptionGroup(invalidRange).Issues,
            issue => issue.Code == "OPTION_GROUP_MINIMUM_EXCEEDS_MAXIMUM");
        Assert.Contains(
            _validator.ValidateOptionGroup(required).Issues,
            issue => issue.Code == "REQUIRED_OPTION_GROUP_MINIMUM_ZERO");
    }

    [Fact]
    public async Task ProductOptionGroup_DuplicateAssignmentIsRejected()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var product = CreateProduct(CreateCategory("Coffee", 0), "Latte", 30m);
        var optionGroup = CreateOptionGroup("Size", OptionSelectionType.Single, 1, 1);
        product.OptionGroups.Add(CreateProductOptionGroup(product, optionGroup));
        product.OptionGroups.Add(CreateProductOptionGroup(product, optionGroup));
        database.Context.Products.Add(product);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public void OptionValue_MustBelongToTheAssignedGlobalGroup()
    {
        var product = CreateProduct(CreateCategory("Coffee", 0), "Latte", 30m);
        var size = CreateOptionGroup("Size", OptionSelectionType.Single, 1, 1);
        var milk = CreateOptionGroup("Milk", OptionSelectionType.Single, 1, 1);
        var sizeValue = CreateOptionValue(size, "Medium");
        var milkValue = CreateOptionValue(milk, "Oat Milk");
        var assignment = CreateProductOptionGroup(product, size);

        var valid = CreateProductOptionValue(assignment, sizeValue);
        var invalid = CreateProductOptionValue(assignment, milkValue);

        Assert.True(_validator.ValidateProductOptionValue(valid).IsValid);
        Assert.Contains(
            _validator.ValidateProductOptionValue(invalid).Issues,
            issue => issue.Code == "OPTION_VALUE_BELONGS_TO_DIFFERENT_GROUP");
    }

    [Fact]
    public async Task ProductOptionValue_DuplicateAssignmentIsRejected()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var product = CreateProduct(CreateCategory("Coffee", 0), "Latte", 30m);
        var optionGroup = CreateOptionGroup("Milk", OptionSelectionType.Single, 1, 1);
        var optionValue = CreateOptionValue(optionGroup, "Oat Milk");
        var assignment = CreateProductOptionGroup(product, optionGroup);
        assignment.Values.Add(CreateProductOptionValue(assignment, optionValue));
        assignment.Values.Add(CreateProductOptionValue(assignment, optionValue));
        product.OptionGroups.Add(assignment);
        database.Context.Products.Add(product);

        await Assert.ThrowsAsync<DbUpdateException>(
            () => database.Context.SaveChangesAsync());
    }

    [Fact]
    public async Task ProductOptionValue_PersistsProductSpecificPriceAndAvailability()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var product = CreateProduct(CreateCategory("Coffee", 0), "Latte", 30m);
        var optionGroup = CreateOptionGroup("Milk", OptionSelectionType.Single, 1, 1);
        var optionValue = CreateOptionValue(optionGroup, "Oat Milk");
        var assignment = CreateProductOptionGroup(product, optionGroup);
        assignment.Values.Add(CreateProductOptionValue(
            assignment,
            optionValue,
            priceModifier: 6.50m,
            isAvailable: false));
        product.OptionGroups.Add(assignment);
        database.Context.Products.Add(product);

        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        var saved = await database.Context.ProductOptionValues.SingleAsync();
        Assert.Equal(6.50m, saved.PriceModifier);
        Assert.False(saved.IsAvailable);
    }

    [Fact]
    public async Task MediaFile_CreatedAtIsAssignedInUtc()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        database.Context.MediaFiles.Add(new MediaFile
        {
            Id = Guid.NewGuid(),
            StorageProvider = "Local",
            StorageKey = "menu/example.jpg",
            OriginalFileName = "example.jpg",
            ContentType = "image/jpeg",
            FileSizeBytes = 1024
        });

        await database.Context.SaveChangesAsync();

        var saved = await database.Context.MediaFiles.SingleAsync();
        Assert.NotEqual(default, saved.CreatedAt);
        Assert.Equal(TimeSpan.Zero, saved.CreatedAt.Offset);
    }

    [Fact]
    public void Defaults_RequiredSingleWithOneAvailableDefaultIsValid()
    {
        var product = CreateOrderableProduct();

        var configuration = _validator.ValidateProductConfiguration(product);
        var availability = _validator.EvaluateOrderability(product);

        Assert.True(configuration.IsValid);
        Assert.True(availability.IsOrderable);
    }

    [Fact]
    public void Defaults_TwoDefaultsInSingleGroupAreRejected()
    {
        var product = CreateOrderableProduct();
        var assignment = product.OptionGroups.Single();
        var second = CreateOptionValue(assignment.OptionGroup, "Large");
        assignment.Values.Add(CreateProductOptionValue(
            assignment,
            second,
            isDefault: true));

        Assert.Contains(
            _validator.ValidateProductConfiguration(product).Issues,
            issue => issue.Code == "SINGLE_GROUP_HAS_MULTIPLE_DEFAULTS");
    }

    [Fact]
    public void Defaults_RequiredGroupWithoutAvailableValueOrDefaultIsNotOrderable()
    {
        var product = CreateOrderableProduct();
        var onlyValue = product.OptionGroups.Single().Values.Single();
        onlyValue.IsAvailable = false;

        var result = _validator.EvaluateOrderability(product);

        Assert.Contains(
            result.Issues,
            issue => issue.Code == "REQUIRED_OPTION_HAS_NO_AVAILABLE_VALUES");
        Assert.Contains(
            result.Issues,
            issue => issue.Code == "REQUIRED_SINGLE_GROUP_HAS_NO_AVAILABLE_DEFAULT");
    }

    [Fact]
    public async Task SoftDelete_ProductIsFilteredButAssignmentsRemainRetrievable()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var product = CreateOrderableProduct();
        database.Context.Products.Add(product);
        await database.Context.SaveChangesAsync();

        product.IsDeleted = true;
        await database.Context.SaveChangesAsync();
        database.Context.ChangeTracker.Clear();

        Assert.Empty(await database.Context.Products.ToListAsync());
        Assert.Single(await database.Context.Products.IgnoreQueryFilters().ToListAsync());
        Assert.Single(
            await database.Context.ProductOptionGroups.IgnoreQueryFilters().ToListAsync());
        Assert.Single(
            await database.Context.ProductOptionValues.IgnoreQueryFilters().ToListAsync());
    }

    [Fact]
    public async Task Concurrency_StaleUpdateThrowsAndDoesNotOverwriteNewerValue()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var category = CreateCategory("Coffee", 0);
        database.Context.Categories.Add(category);
        await database.Context.SaveChangesAsync();

        await using var firstContext = database.CreateContext();
        await using var secondContext = database.CreateContext();
        var first = await firstContext.Categories.SingleAsync();
        var stale = await secondContext.Categories.SingleAsync();

        first.Description = "First saved value";
        await firstContext.SaveChangesAsync();
        stale.Description = "Stale value";

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(
            () => secondContext.SaveChangesAsync());

        await using var verificationContext = database.CreateContext();
        Assert.Equal(
            "First saved value",
            (await verificationContext.Categories.SingleAsync()).Description);
    }

    [Fact]
    public async Task DevelopmentSeeder_IsIdempotentAndIncludesRequiredScenarios()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var seeder = new DevelopmentMenuSeeder(
            database.Context,
            new TestHostEnvironment(Environments.Development),
            NullLogger<DevelopmentMenuSeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);
        var firstCounts = await ReadSeedCountsAsync(database.Context);
        await seeder.SeedAsync(CancellationToken.None);
        var secondCounts = await ReadSeedCountsAsync(database.Context);

        Assert.Equal((5, 5, 3, 9), firstCounts);
        Assert.Equal(firstCounts, secondCounts);
        Assert.Contains(
            await database.Context.Products.ToListAsync(),
            product => !product.IsAvailable);
        Assert.Contains(
            await database.Context.Products.Include(product => product.OptionGroups).ToListAsync(),
            product => product.OptionGroups.Count == 0);
        Assert.Contains(
            await database.Context.ProductOptionValues.ToListAsync(),
            value => !value.IsAvailable);
        Assert.Contains(
            await database.Context.ProductOptionValues.ToListAsync(),
            value => value.IsDefault && value.IsAvailable);
    }

    [Fact]
    public async Task DevelopmentSeeder_DoesNotRunInProduction()
    {
        await using var database = await RelationalMenuDatabase.CreateAsync();
        var seeder = new DevelopmentMenuSeeder(
            database.Context,
            new TestHostEnvironment(Environments.Production),
            NullLogger<DevelopmentMenuSeeder>.Instance);

        await seeder.SeedAsync(CancellationToken.None);

        Assert.Empty(await database.Context.Categories.ToListAsync());
        Assert.Empty(await database.Context.Products.ToListAsync());
        Assert.Empty(await database.Context.OptionGroups.ToListAsync());
    }

    private static async Task<(int Categories, int Products, int Groups, int Values)>
        ReadSeedCountsAsync(MoodPickupDbContext context)
    {
        return (
            await context.Categories.CountAsync(),
            await context.Products.CountAsync(),
            await context.OptionGroups.CountAsync(),
            await context.OptionValues.CountAsync());
    }

    private static Category CreateCategory(string name, int displayOrder)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayOrder = displayOrder,
            IsVisible = true
        };
    }

    private static Product CreateProduct(
        Category category,
        string name,
        decimal basePrice,
        int displayOrder = 0)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = name,
            BasePrice = basePrice,
            DisplayOrder = displayOrder,
            IsAvailable = true,
            IsVisible = true
        };
    }

    private static OptionGroup CreateOptionGroup(
        string name,
        OptionSelectionType selectionType,
        int minimumSelections,
        int maximumSelections)
    {
        return new OptionGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            SelectionType = selectionType,
            DefaultIsRequired = minimumSelections > 0,
            DefaultMinimumSelections = minimumSelections,
            DefaultMaximumSelections = maximumSelections,
            IsActive = true
        };
    }

    private static OptionValue CreateOptionValue(OptionGroup group, string name)
    {
        var value = new OptionValue
        {
            Id = Guid.NewGuid(),
            OptionGroupId = group.Id,
            OptionGroup = group,
            Name = name,
            IsActive = true
        };
        group.Values.Add(value);
        return value;
    }

    private static ProductOptionGroup CreateProductOptionGroup(
        Product product,
        OptionGroup group)
    {
        return new ProductOptionGroup
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            OptionGroupId = group.Id,
            OptionGroup = group,
            IsRequired = true,
            MinimumSelections = 1,
            MaximumSelections = 1,
            IsActive = true
        };
    }

    private static ProductOptionValue CreateProductOptionValue(
        ProductOptionGroup assignment,
        OptionValue value,
        decimal priceModifier = 0m,
        bool isDefault = false,
        bool isAvailable = true)
    {
        return new ProductOptionValue
        {
            Id = Guid.NewGuid(),
            ProductOptionGroupId = assignment.Id,
            ProductOptionGroup = assignment,
            OptionValueId = value.Id,
            OptionValue = value,
            PriceModifier = priceModifier,
            IsDefault = isDefault,
            IsAvailable = isAvailable
        };
    }

    private static Product CreateOrderableProduct()
    {
        var category = CreateCategory("Coffee", 0);
        var product = CreateProduct(category, "Cappuccino", 28m);
        var optionGroup = CreateOptionGroup(
            "Size",
            OptionSelectionType.Single,
            minimumSelections: 1,
            maximumSelections: 1);
        var optionValue = CreateOptionValue(optionGroup, "Medium");
        var assignment = CreateProductOptionGroup(product, optionGroup);
        assignment.Values.Add(CreateProductOptionValue(
            assignment,
            optionValue,
            isDefault: true));
        product.OptionGroups.Add(assignment);
        return product;
    }

    private sealed class RelationalMenuDatabase : IAsyncDisposable
    {
        private readonly SqliteConnection _connection;

        private RelationalMenuDatabase(SqliteConnection connection)
        {
            _connection = connection;
            Context = CreateContext();
        }

        public MoodPickupDbContext Context { get; }

        public static async Task<RelationalMenuDatabase> CreateAsync()
        {
            var connection = new SqliteConnection(
                "Data Source=:memory:;Foreign Keys=True");
            await connection.OpenAsync();
            var database = new RelationalMenuDatabase(connection);
            await database.Context.Database.EnsureCreatedAsync();
            return database;
        }

        public MoodPickupDbContext CreateContext()
        {
            var options = new DbContextOptionsBuilder<MoodPickupDbContext>()
                .UseSqlite(_connection)
                .Options;
            return new MoodPickupDbContext(options);
        }

        public async ValueTask DisposeAsync()
        {
            await Context.DisposeAsync();
            await _connection.DisposeAsync();
        }
    }

    private sealed class TestHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "MoodPickup.Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } =
            new NullFileProvider();
    }
}
