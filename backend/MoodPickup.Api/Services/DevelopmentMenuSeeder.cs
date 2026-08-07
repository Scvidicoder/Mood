using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class DevelopmentMenuSeeder(
    MoodPickupDbContext dbContext,
    IHostEnvironment environment,
    ILogger<DevelopmentMenuSeeder> logger) : IDevelopmentMenuSeeder
{
    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!environment.IsDevelopment())
        {
            return;
        }

        if (await HasAnyMenuDataAsync(cancellationToken))
        {
            logger.LogInformation(
                "Development menu seed skipped because menu data already exists.");
            return;
        }

        var categories = CreateCategories();
        var optionGroups = CreateOptionGroups();
        var products = CreateProducts(categories);

        ConfigureCappuccino(
            products.Single(product => product.Name == "Cappuccino"),
            optionGroups);

        dbContext.Categories.AddRange(categories);
        dbContext.OptionGroups.AddRange(optionGroups);
        dbContext.Products.AddRange(products);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Created development menu seed with {CategoryCount} categories, " +
            "{ProductCount} products, and {OptionGroupCount} option groups.",
            categories.Count,
            products.Count,
            optionGroups.Count);
    }

    private async Task<bool> HasAnyMenuDataAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Categories
                   .IgnoreQueryFilters()
                   .AnyAsync(cancellationToken) ||
               await dbContext.Products
                   .IgnoreQueryFilters()
                   .AnyAsync(cancellationToken) ||
               await dbContext.OptionGroups
                   .IgnoreQueryFilters()
                   .AnyAsync(cancellationToken) ||
               await dbContext.OptionValues
                   .IgnoreQueryFilters()
                   .AnyAsync(cancellationToken);
    }

    private static List<Category> CreateCategories()
    {
        return
        [
            CreateCategory("Coffee", 0),
            CreateCategory("Tea", 1),
            CreateCategory("Cold Drinks", 2),
            CreateCategory("Breakfast", 3),
            CreateCategory("Desserts", 4)
        ];
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

    private static List<OptionGroup> CreateOptionGroups()
    {
        var size = CreateOptionGroup(
            "Size",
            OptionSelectionType.Single,
            isRequired: true,
            minimumSelections: 1,
            maximumSelections: 1,
            displayOrder: 0);
        AddValue(size, "Small", 0);
        AddValue(size, "Medium", 1);
        AddValue(size, "Large", 2);

        var milk = CreateOptionGroup(
            "Milk",
            OptionSelectionType.Single,
            isRequired: true,
            minimumSelections: 1,
            maximumSelections: 1,
            displayOrder: 1);
        AddValue(milk, "Regular Milk", 0);
        AddValue(milk, "Oat Milk", 1);
        AddValue(milk, "Coconut Milk", 2);

        var syrups = CreateOptionGroup(
            "Syrups",
            OptionSelectionType.Multiple,
            isRequired: false,
            minimumSelections: 0,
            maximumSelections: 3,
            displayOrder: 2);
        AddValue(syrups, "Vanilla", 0);
        AddValue(syrups, "Caramel", 1);
        AddValue(syrups, "Hazelnut", 2);

        return [size, milk, syrups];
    }

    private static OptionGroup CreateOptionGroup(
        string name,
        OptionSelectionType selectionType,
        bool isRequired,
        int minimumSelections,
        int maximumSelections,
        int displayOrder)
    {
        return new OptionGroup
        {
            Id = Guid.NewGuid(),
            Name = name,
            SelectionType = selectionType,
            DefaultIsRequired = isRequired,
            DefaultMinimumSelections = minimumSelections,
            DefaultMaximumSelections = maximumSelections,
            DisplayOrder = displayOrder,
            IsActive = true
        };
    }

    private static void AddValue(
        OptionGroup optionGroup,
        string name,
        int displayOrder)
    {
        optionGroup.Values.Add(new OptionValue
        {
            Id = Guid.NewGuid(),
            OptionGroupId = optionGroup.Id,
            OptionGroup = optionGroup,
            Name = name,
            DisplayOrder = displayOrder,
            IsActive = true
        });
    }

    private static List<Product> CreateProducts(IReadOnlyCollection<Category> categories)
    {
        var categoriesByName = categories.ToDictionary(
            category => category.Name,
            StringComparer.Ordinal);

        return
        [
            CreateProduct(
                categoriesByName["Coffee"],
                "Cappuccino",
                "Espresso with steamed milk.",
                28m,
                0,
                isAvailable: true,
                defaultVolumeMilliliters: 250,
                defaultCalories: 120),
            CreateProduct(
                categoriesByName["Coffee"],
                "Latte",
                "Espresso with extra steamed milk.",
                30m,
                1,
                isAvailable: false,
                defaultVolumeMilliliters: 300,
                defaultCalories: 150),
            CreateProduct(
                categoriesByName["Coffee"],
                "Americano",
                "Espresso with hot water.",
                22m,
                2,
                isAvailable: true,
                defaultVolumeMilliliters: 250,
                defaultCalories: 10),
            CreateProduct(
                categoriesByName["Desserts"],
                "Cheesecake",
                "Development dessert sample.",
                35m,
                0,
                isAvailable: true,
                defaultWeightGrams: 140,
                defaultCalories: 420),
            CreateProduct(
                categoriesByName["Breakfast"],
                "Croissant",
                "Development breakfast sample.",
                18m,
                0,
                isAvailable: true,
                defaultWeightGrams: 80,
                defaultCalories: 310)
        ];
    }

    private static Product CreateProduct(
        Category category,
        string name,
        string shortDescription,
        decimal basePrice,
        int displayOrder,
        bool isAvailable,
        int? defaultWeightGrams = null,
        int? defaultVolumeMilliliters = null,
        int? defaultCalories = null)
    {
        return new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = name,
            ShortDescription = shortDescription,
            BasePrice = basePrice,
            DefaultWeightGrams = defaultWeightGrams,
            DefaultVolumeMilliliters = defaultVolumeMilliliters,
            DefaultCalories = defaultCalories,
            IsAvailable = isAvailable,
            IsVisible = true,
            DisplayOrder = displayOrder
        };
    }

    private static void ConfigureCappuccino(
        Product cappuccino,
        IReadOnlyCollection<OptionGroup> optionGroups)
    {
        var groupsByName = optionGroups.ToDictionary(
            group => group.Name,
            StringComparer.Ordinal);

        var sizeAssignment = AddGroup(
            cappuccino,
            groupsByName["Size"],
            isRequired: true,
            minimumSelections: 1,
            maximumSelections: 1,
            displayOrder: 0);
        AddAssignedValue(
            sizeAssignment,
            FindValue(groupsByName["Size"], "Small"),
            priceModifier: 0m,
            isDefault: false,
            isAvailable: true,
            displayOrder: 0,
            volumeMilliliters: 200,
            calories: 100);
        AddAssignedValue(
            sizeAssignment,
            FindValue(groupsByName["Size"], "Medium"),
            priceModifier: 4m,
            isDefault: true,
            isAvailable: true,
            displayOrder: 1,
            volumeMilliliters: 300,
            calories: 140);
        AddAssignedValue(
            sizeAssignment,
            FindValue(groupsByName["Size"], "Large"),
            priceModifier: 8m,
            isDefault: false,
            isAvailable: true,
            displayOrder: 2,
            volumeMilliliters: 450,
            calories: 190);

        var milkAssignment = AddGroup(
            cappuccino,
            groupsByName["Milk"],
            isRequired: true,
            minimumSelections: 1,
            maximumSelections: 1,
            displayOrder: 1);
        AddAssignedValue(
            milkAssignment,
            FindValue(groupsByName["Milk"], "Regular Milk"),
            priceModifier: 0m,
            isDefault: true,
            isAvailable: true,
            displayOrder: 0);
        AddAssignedValue(
            milkAssignment,
            FindValue(groupsByName["Milk"], "Oat Milk"),
            priceModifier: 6m,
            isDefault: false,
            isAvailable: true,
            displayOrder: 1);
        AddAssignedValue(
            milkAssignment,
            FindValue(groupsByName["Milk"], "Coconut Milk"),
            priceModifier: 6m,
            isDefault: false,
            isAvailable: false,
            displayOrder: 2);

        var syrupAssignment = AddGroup(
            cappuccino,
            groupsByName["Syrups"],
            isRequired: false,
            minimumSelections: 0,
            maximumSelections: 3,
            displayOrder: 2);

        foreach (var optionValue in groupsByName["Syrups"].Values.OrderBy(value => value.DisplayOrder))
        {
            AddAssignedValue(
                syrupAssignment,
                optionValue,
                priceModifier: 3m,
                isDefault: false,
                isAvailable: true,
                displayOrder: optionValue.DisplayOrder);
        }
    }

    private static ProductOptionGroup AddGroup(
        Product product,
        OptionGroup optionGroup,
        bool isRequired,
        int minimumSelections,
        int maximumSelections,
        int displayOrder)
    {
        var assignment = new ProductOptionGroup
        {
            Id = Guid.NewGuid(),
            ProductId = product.Id,
            Product = product,
            OptionGroupId = optionGroup.Id,
            OptionGroup = optionGroup,
            IsRequired = isRequired,
            MinimumSelections = minimumSelections,
            MaximumSelections = maximumSelections,
            DisplayOrder = displayOrder,
            IsActive = true
        };
        product.OptionGroups.Add(assignment);
        return assignment;
    }

    private static void AddAssignedValue(
        ProductOptionGroup productOptionGroup,
        OptionValue optionValue,
        decimal priceModifier,
        bool isDefault,
        bool isAvailable,
        int displayOrder,
        int? volumeMilliliters = null,
        int? calories = null)
    {
        productOptionGroup.Values.Add(new ProductOptionValue
        {
            Id = Guid.NewGuid(),
            ProductOptionGroupId = productOptionGroup.Id,
            ProductOptionGroup = productOptionGroup,
            OptionValueId = optionValue.Id,
            OptionValue = optionValue,
            PriceModifier = priceModifier,
            IsDefault = isDefault,
            IsAvailable = isAvailable,
            DisplayOrder = displayOrder,
            VolumeMilliliters = volumeMilliliters,
            Calories = calories
        });
    }

    private static OptionValue FindValue(OptionGroup group, string name)
    {
        return group.Values.Single(value => value.Name == name);
    }
}
