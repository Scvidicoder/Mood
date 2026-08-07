using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MoodPickup.Api.Data;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Tests;

public sealed class MoodPickupApiFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"MoodPickupTests-{Guid.NewGuid():N}";

    public TestTimeProvider TimeProvider { get; } = new();

    public TestTelegramOtpSender OtpSender { get; } = new();

    public TestTelegramBotClient BotClient { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureAppConfiguration((_, configurationBuilder) =>
        {
            configurationBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=1;Database=moodpickup;Username=test;Password=test;Timeout=1;Command Timeout=1",
                ["AllowedOrigins"] = "https://localhost",
                ["Database:ApplyMigrationsOnStartup"] = "false",
                ["Jwt:Issuer"] = "MoodPickup.Tests",
                ["Jwt:Audience"] = "MoodPickup.Tests.Client",
                ["Jwt:SigningKey"] =
                    "moodpickup-testing-signing-key-at-least-thirty-two-characters",
                ["Otp:HashKey"] =
                    "moodpickup-testing-otp-hash-key-at-least-thirty-two-characters",
                ["Telegram:Enabled"] = "true",
                ["Telegram:BotToken"] = "123456:test-token-not-real",
                ["Telegram:BotUsername"] = "test_bot",
                ["Telegram:WebhookSecret"] = "testing_webhook_secret",
                ["Telegram:PublicBaseUrl"] = "https://api.example.test",
                ["Telegram:RegisterWebhookOnStartup"] = "false",
                ["Telegram:UseDevelopmentSender"] = "false",
                ["PasswordPolicy:CommonPasswords:0"] = "password",
                ["AdministratorSeed:Enabled"] = "true",
                ["AdministratorSeed:Username"] = "admin",
                ["AdministratorSeed:Password"] = "TestingAdmin1!",
                ["AdministratorSeed:FullName"] = "Test Administrator"
            });
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<MoodPickupDbContext>>();
            services.RemoveAll<MoodPickupDbContext>();
            services.AddDbContext<MoodPickupDbContext>(options =>
                options.UseInMemoryDatabase(_databaseName));

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(TimeProvider);

            services.RemoveAll<ITelegramOtpSender>();
            services.AddSingleton<ITelegramOtpSender>(OtpSender);
            services.RemoveAll<ITelegramBotClient>();
            services.AddSingleton<ITelegramBotClient>(BotClient);
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

    public async Task ResetAuthenticationStateAsync()
    {
        TimeProvider.Reset();
        OtpSender.Clear();
        BotClient.Clear();

        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<MoodPickupDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.EnsureCreatedAsync();
        var seeder = scope.ServiceProvider.GetRequiredService<AdministratorSeeder>();
        await seeder.SeedAsync(CancellationToken.None);
    }
}
