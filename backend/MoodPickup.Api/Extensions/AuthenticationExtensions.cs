using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MoodPickup.Api.Authorization;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Infrastructure.Telegram;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;
using MoodPickup.Api.Services.Telegram;

namespace MoodPickup.Api.Extensions;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddMoodPickupAuthentication(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var jwtOptions = configuration
            .GetRequiredSection(JwtOptions.SectionName)
            .Get<JwtOptions>()
            ?? throw new InvalidOperationException("Jwt configuration is required.");
        var telegramOptions = configuration
            .GetRequiredSection(TelegramOptions.SectionName)
            .Get<TelegramOptions>()
            ?? new TelegramOptions();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                    ValidateLifetime = true,
                    RequireExpirationTime = true,
                    RequireSignedTokens = true,
                    ClockSkew = TimeSpan.FromSeconds(jwtOptions.ClockSkewSeconds),
                    NameClaimType = "unique_name",
                    RoleClaimType = "roles"
                };
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = context =>
                    {
                        var accessToken = context.Request.Query["access_token"];

                        if (!string.IsNullOrWhiteSpace(accessToken) &&
                            context.HttpContext.Request.Path.StartsWithSegments(
                                "/hubs/notifications"))
                        {
                            context.Token = accessToken;
                        }

                        return Task.CompletedTask;
                    }
                };
            });

        services.AddScoped<EmployeeAccessStateService>();
        services.AddScoped<IAuthorizationHandler, AccountTypeAuthorizationHandler>();
        services.AddScoped<IAuthorizationHandler, EmployeePermissionAuthorizationHandler>();
        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthenticationConstants.Policies.Customer,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new AccountTypeRequirement(AuthenticationConstants.AccountTypes.Customer)));
            options.AddPolicy(
                AuthenticationConstants.Policies.Employee,
                policy => policy
                    .RequireAuthenticatedUser()
                    .AddRequirements(
                        new AccountTypeRequirement(AuthenticationConstants.AccountTypes.Employee)));

            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanReceiveOrders,
                EmployeePermissionCatalog.ConfirmOrders,
                AuthenticationConstants.Roles.OrderReception);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanWorkKitchen,
                EmployeePermissionCatalog.StartPreparing,
                AuthenticationConstants.Roles.Kitchen);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanViewKitchen,
                EmployeePermissionCatalog.ViewKitchen,
                AuthenticationConstants.Roles.Kitchen,
                AuthenticationConstants.Roles.Cashier,
                AuthenticationConstants.Roles.Manager,
                AuthenticationConstants.Roles.Pickup);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanIssueOrders,
                EmployeePermissionCatalog.CompleteOrders,
                AuthenticationConstants.Roles.Pickup,
                AuthenticationConstants.Roles.Cashier);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageMenu,
                EmployeePermissionCatalog.ManageProducts,
                AuthenticationConstants.Roles.MenuManager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageEmployees,
                EmployeePermissionCatalog.ManageEmployees);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageCafeSettings,
                EmployeePermissionCatalog.ManageSettings);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanViewAuditLog,
                EmployeePermissionCatalog.ViewReports);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageOrders,
                EmployeePermissionCatalog.ViewOrders,
                AuthenticationConstants.Roles.Cashier,
                AuthenticationConstants.Roles.Manager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanViewOrders,
                EmployeePermissionCatalog.ViewOrders,
                AuthenticationConstants.Roles.Cashier,
                AuthenticationConstants.Roles.Manager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanConfirmOrders,
                EmployeePermissionCatalog.ConfirmOrders,
                AuthenticationConstants.Roles.Cashier,
                AuthenticationConstants.Roles.Manager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanRejectOrders,
                EmployeePermissionCatalog.RejectOrders,
                AuthenticationConstants.Roles.Cashier,
                AuthenticationConstants.Roles.Manager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanCompleteOrders,
                EmployeePermissionCatalog.CompleteOrders,
                AuthenticationConstants.Roles.Pickup,
                AuthenticationConstants.Roles.Cashier);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanStartPreparing,
                EmployeePermissionCatalog.StartPreparing,
                AuthenticationConstants.Roles.Kitchen);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanMarkReady,
                EmployeePermissionCatalog.MarkReady,
                AuthenticationConstants.Roles.Kitchen);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageCategories,
                EmployeePermissionCatalog.ManageCategories,
                AuthenticationConstants.Roles.MenuManager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageProducts,
                EmployeePermissionCatalog.ManageProducts,
                AuthenticationConstants.Roles.MenuManager);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageOptions,
                EmployeePermissionCatalog.ManageOptions,
                AuthenticationConstants.Roles.MenuManager);
        });

        services.AddScoped<AuthenticationHashing>();
        services.AddSingleton<IPasswordPolicyValidator, PasswordPolicyValidator>();
        services.AddScoped<ITokenIssuer, HmacJwtTokenIssuer>();
        services.AddScoped<RefreshTokenService>();
        services.AddScoped<CustomerAuthenticationService>();
        services.AddScoped<EmployeeAuthenticationService>();
        services.AddScoped<SessionService>();
        services.AddScoped<AuthenticationCookieService>();
        services.AddScoped<DoubleSubmitCsrfFilter>();
        services.AddScoped<AdministratorSeeder>();
        services.AddSingleton<TelegramStartupState>();
        services.AddSingleton<TelegramAbuseLimiter>();
        services.AddSingleton<TelegramMessageProvider>();
        services.AddScoped<ITelegramUpdateHandler, TelegramUpdateHandler>();
        services.AddScoped<TelegramWebhookSecretFilter>();
        services.AddHostedService<TelegramWebhookRegistrationService>();
        services.AddHostedService<TelegramProcessedUpdateCleanupService>();
        services
            .AddHttpClient<ITelegramBotClient, TelegramBotApiClient>(
                client =>
                {
                    client.BaseAddress = new Uri("https://api.telegram.org/");
                    client.Timeout = TimeSpan.FromSeconds(
                        telegramOptions.ApiTimeoutSeconds);
                })
            .RemoveAllLoggers();

        if (environment.IsDevelopment() &&
            telegramOptions.UseDevelopmentSender)
        {
            services.AddScoped<ITelegramOtpSender, DevelopmentTelegramOtpSender>();
        }
        else if (telegramOptions.Enabled)
        {
            services.AddScoped<ITelegramOtpSender, TelegramOtpSender>();
        }
        else
        {
            services.AddScoped<ITelegramOtpSender, UnavailableTelegramOtpSender>();
        }

        return services;
    }

    private static void AddEmployeePolicy(
        AuthorizationOptions options,
        string policyName,
        string permission,
        params string[] roles)
    {
        options.AddPolicy(
            policyName,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new EmployeePermissionRequirement(permission, roles)));
    }
}
