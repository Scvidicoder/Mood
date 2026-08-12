using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Payments;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class PostgresMoodPickupApiFactory : WebApplicationFactory<Program>
{
    public const string ConnectionStringVariable =
        "MOODPICKUP_POSTGRES_TEST_CONNECTION";

    private readonly string _connectionString =
        Environment.GetEnvironmentVariable(ConnectionStringVariable)
        ?? "Host=127.0.0.1;Port=1;Database=missing;Username=missing;Password=missing";
    private readonly string _mediaRootPath = Path.Combine(
        Path.GetTempPath(),
        $"moodpickup-media-tests-{Guid.NewGuid():N}");

    public TestTimeProvider TimeProvider { get; } = new();

    public TestTelegramOtpSender OtpSender { get; } = new();

    public TestAlifPaymentProvider PaymentProvider { get; } = new();

    public bool FailAuditWrites { get; set; }

    public string MediaRootPath => _mediaRootPath;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _connectionString,
                ["AllowedOrigins"] = "https://localhost",
                ["Database:ApplyMigrationsOnStartup"] = "true",
                ["Jwt:Issuer"] = "MoodPickup",
                ["Jwt:Audience"] = "MoodPickup.Client",
                ["Jwt:SigningKey"] =
                    "moodpickup-development-signing-key-change-before-production-2026",
                ["Otp:HashKey"] =
                    "moodpickup-development-otp-hash-key-change-before-production-2026",
                ["Otp:TelegramBotUrl"] = "https://t.me/test_bot",
                ["PasswordPolicy:CommonPasswords:0"] = "password",
                ["MediaStorage:Provider"] = "Local",
                ["MediaStorage:RootPath"] = _mediaRootPath,
                ["MediaStorage:PublicBasePath"] = "/media",
                ["MediaStorage:MaximumFileSizeBytes"] = "131072",
                ["MediaStorage:MaximumImageWidth"] = "64",
                ["MediaStorage:MaximumImageHeight"] = "64",
                ["MediaStorage:MaximumDecodedImageBytes"] = "1048576",
                ["AdministratorSeed:Enabled"] = "true",
                ["AdministratorSeed:Username"] = "admin",
                ["AdministratorSeed:Password"] = "TestingAdmin1!",
                ["AdministratorSeed:FullName"] = "PostgreSQL Test Administrator",
                ["Payment:Provider"] = "Alif",
                ["Alif:Enabled"] = "true",
                ["Alif:Environment"] = "Sandbox",
                ["Alif:Key"] = "44444444",
                ["Alif:Password"] = "cztef62wrwcysyubbbdnhlk1rs2cztfsqgwww7j0",
                ["Alif:CallbackUrl"] = "https://localhost/api/v1/payments/alif/callback",
                ["Alif:ReturnUrl"] = "https://localhost/payment/result",
                ["Alif:Gate"] = "km"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MoodPickupDbContext>>();
            services.RemoveAll<MoodPickupDbContext>();
            services.AddDbContext<MoodPickupDbContext>(options =>
                options.UseNpgsql(_connectionString));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);
            services.RemoveAll<ITelegramOtpSender>();
            services.AddSingleton<ITelegramOtpSender>(OtpSender);
            services.RemoveAll<IEmployeeAuditService>();
            services.AddScoped<IEmployeeAuditService>(provider =>
                new SwitchableTestAuditService(
                    this,
                    new EmployeeAuditService(
                        provider.GetRequiredService<MoodPickupDbContext>(),
                        provider.GetRequiredService<ICurrentUserContext>())));
            services.RemoveAll<IPaymentProvider>();
            services.AddSingleton<IPaymentProvider>(PaymentProvider);
        });
    }

    public HttpClient CreateSecureClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = false
        });
    }

    public async Task ResetAsync(bool seedMenu = true)
    {
        FailAuditWrites = false;
        TimeProvider.Reset();
        OtpSender.Clear();
        PaymentProvider.Reset();
        ResetMediaStorage();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        await scope.ServiceProvider
            .GetRequiredService<AdministratorSeeder>()
            .SeedAsync(CancellationToken.None);

        if (seedMenu)
        {
            await SeedMenuAsync(dbContext);
        }
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            ResetMediaStorage();
        }
    }

    public async Task<string> GetAdministratorTokenAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        var administrator = await dbContext.Employees
            .SingleAsync(employee => employee.Username == "admin");
        return scope.ServiceProvider
            .GetRequiredService<ITokenIssuer>()
            .IssueEmployeeAccessToken(
                administrator,
                [AuthenticationConstants.Roles.Administrator])
            .Value;
    }

    public async Task<string> CreateEmployeeTokenAsync(
        string username,
        params string[] roles)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            Username = username,
            FullName = $"{username} employee",
            IsAdmin = roles.Contains(AuthenticationConstants.Roles.Administrator),
            MustChangePassword = false,
            PasswordHash = "not-used-by-token-test",
            CreatedAt = TimeProvider.GetUtcNow(),
            UpdatedAt = TimeProvider.GetUtcNow()
        };

        foreach (var roleName in roles)
        {
            var role = await dbContext.Roles.SingleAsync(role => role.Name == roleName);
            employee.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = employee.Id,
                Employee = employee,
                RoleId = role.Id,
                Role = role
            });
        }

        dbContext.Employees.Add(employee);
        await dbContext.SaveChangesAsync();
        return scope.ServiceProvider
            .GetRequiredService<ITokenIssuer>()
            .IssueEmployeeAccessToken(employee, roles)
            .Value;
    }

    public async Task<string> CreateCustomerTokenAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        var customer = new Customer
        {
            Id = Guid.NewGuid(),
            Name = "API Customer",
            PhoneNumber = $"+992{Random.Shared.NextInt64(100000000, 999999999)}",
            CreatedAt = TimeProvider.GetUtcNow(),
            UpdatedAt = TimeProvider.GetUtcNow()
        };
        dbContext.Customers.Add(customer);
        await dbContext.SaveChangesAsync();
        return scope.ServiceProvider
            .GetRequiredService<ITokenIssuer>()
            .IssueCustomerAccessToken(customer)
            .Value;
    }

    public async Task<T> ReadDatabaseAsync<T>(
        Func<MoodPickupDbContext, Task<T>> read)
    {
        await using var scope = Services.CreateAsyncScope();
        return await read(scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>());
    }

    private static async Task SeedMenuAsync(MoodPickupDbContext dbContext)
    {
        var coffee = Category("Coffee", 1, visible: true);
        var hiddenCategory = Category("Hidden Category", 2, visible: false);
        var emptyCategory = Category("Empty Category", 3, visible: true);
        var deletedCategory = Category("Deleted Category", 4, visible: true);
        deletedCategory.IsDeleted = true;

        var size = new OptionGroup
        {
            Id = Guid.NewGuid(),
            Name = "Size",
            SelectionType = OptionSelectionType.Single,
            DefaultIsRequired = true,
            DefaultMinimumSelections = 1,
            DefaultMaximumSelections = 1,
            IsActive = true
        };
        var small = OptionValue(size, "Small", 0);
        var large = OptionValue(size, "Large", 1);
        var unassigned = OptionValue(size, "Unassigned", 2);
        var deletedValue = OptionValue(size, "Deleted Value", 3);
        deletedValue.IsDeleted = true;

        var cappuccino = Product(coffee, "Cappuccino", 22m, 1);
        cappuccino.Description = "Coffee with steamed milk";
        var sizeAssignment = new ProductOptionGroup
        {
            Id = Guid.NewGuid(),
            ProductId = cappuccino.Id,
            Product = cappuccino,
            OptionGroupId = size.Id,
            OptionGroup = size,
            IsRequired = true,
            MinimumSelections = 1,
            MaximumSelections = 1,
            DisplayOrder = 1,
            IsActive = true
        };
        sizeAssignment.Values.Add(AssignedValue(
            sizeAssignment,
            small,
            2m,
            isDefault: true,
            isAvailable: true,
            displayOrder: 1));
        sizeAssignment.Values.Add(AssignedValue(
            sizeAssignment,
            large,
            8m,
            isDefault: false,
            isAvailable: false,
            displayOrder: 2));
        sizeAssignment.Values.Add(AssignedValue(
            sizeAssignment,
            deletedValue,
            1m,
            isDefault: false,
            isAvailable: true,
            displayOrder: 3));
        cappuccino.OptionGroups.Add(sizeAssignment);

        var americano = Product(coffee, "Americano", 18m, 2);
        var unavailable = Product(coffee, "Seasonal Latte", 25m, 3);
        unavailable.IsAvailable = false;
        var hiddenProduct = Product(coffee, "Hidden Product", 10m, 4);
        hiddenProduct.IsVisible = false;
        var deletedProduct = Product(coffee, "Deleted Product", 10m, 5);
        deletedProduct.IsDeleted = true;
        _ = Product(hiddenCategory, "Hidden Category Product", 10m, 1);
        _ = Product(deletedCategory, "Deleted Category Product", 10m, 1);

        dbContext.AddRange(
            coffee,
            hiddenCategory,
            emptyCategory,
            deletedCategory,
            size);
        await dbContext.SaveChangesAsync();
    }

    private void ResetMediaStorage()
    {
        var fullPath = Path.GetFullPath(_mediaRootPath);
        var temporaryRoot = Path.GetFullPath(Path.GetTempPath());
        if (!fullPath.StartsWith(
                temporaryRoot,
                OperatingSystem.IsWindows()
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The media test path must stay inside the system temporary directory.");
        }

        if (Directory.Exists(fullPath))
        {
            Directory.Delete(fullPath, recursive: true);
        }
    }

    private static Category Category(string name, int order, bool visible)
    {
        return new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            DisplayOrder = order,
            IsVisible = visible
        };
    }

    private static Product Product(
        Category category,
        string name,
        decimal price,
        int order)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            CategoryId = category.Id,
            Category = category,
            Name = name,
            ShortDescription = $"{name} short description",
            BasePrice = price,
            DisplayOrder = order,
            IsAvailable = true,
            IsVisible = true
        };
        category.Products.Add(product);
        return product;
    }

    private static OptionValue OptionValue(
        OptionGroup group,
        string name,
        int order)
    {
        var value = new OptionValue
        {
            Id = Guid.NewGuid(),
            OptionGroupId = group.Id,
            OptionGroup = group,
            Name = name,
            DisplayOrder = order,
            IsActive = true
        };
        group.Values.Add(value);
        return value;
    }

    private static ProductOptionValue AssignedValue(
        ProductOptionGroup assignment,
        OptionValue value,
        decimal modifier,
        bool isDefault,
        bool isAvailable,
        int displayOrder)
    {
        return new ProductOptionValue
        {
            Id = Guid.NewGuid(),
            ProductOptionGroupId = assignment.Id,
            ProductOptionGroup = assignment,
            OptionValueId = value.Id,
            OptionValue = value,
            PriceModifier = modifier,
            IsDefault = isDefault,
            IsAvailable = isAvailable,
            DisplayOrder = displayOrder
        };
    }

    private sealed class SwitchableTestAuditService(
        PostgresMoodPickupApiFactory factory,
        IEmployeeAuditService inner) : IEmployeeAuditService
    {
        public Task RecordAsync(
            string actionType,
            string entityType,
            Guid entityId,
            string description,
            object? oldValues,
            object? newValues,
            CancellationToken cancellationToken)
        {
            if (factory.FailAuditWrites)
            {
                throw new InvalidOperationException("Injected audit failure.");
            }

            return inner.RecordAsync(
                actionType,
                entityType,
                entityId,
                description,
                oldValues,
                newValues,
                cancellationToken);
        }
    }
}

public sealed class TestAlifPaymentProvider : IPaymentProvider
{
    public PaymentProvider Provider => PaymentProvider.Alif;

    public PaymentProviderStatusResult? CheckResult { get; set; }

    public int RefundCalls { get; private set; }

    public Task<PaymentLaunchResponse> CreatePaymentLaunchAsync(
        PaymentProviderLaunchRequest request,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(new PaymentLaunchResponse(
            request.PaymentId,
            "https://test-web.alif.tj/",
            HttpMethod.Post.Method,
            new Dictionary<string, string>
            {
                ["key"] = "44444444",
                ["token"] = "test-per-payment-token",
                ["orderId"] = request.ProviderOrderId,
                ["amount"] = AlifSignatureService.FormatAmount(request.Amount),
                ["callbackUrl"] = "https://localhost/api/v1/payments/alif/callback",
                ["returnUrl"] = $"https://localhost/payment/result?paymentId={request.PaymentId:D}",
                ["phone"] = request.CustomerPhoneNumber[4..],
                ["gate"] = "km"
            }));
    }

    public Task<PaymentProviderStatusResult> CheckPaymentStatusAsync(
        string providerOrderId,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(CheckResult ?? new PaymentProviderStatusResult(
            providerOrderId,
            "test-transaction",
            "pending",
            0m,
            PaymentStatus.Pending,
            null));
    }

    public Task<PaymentProviderRefundResult> RefundAsync(
        PaymentProviderRefundRequest request,
        CancellationToken cancellationToken)
    {
        RefundCalls++;
        throw new InvalidOperationException("PostgreSQL tests must not send refunds.");
    }

    public void Reset()
    {
        CheckResult = null;
        RefundCalls = 0;
    }
}
