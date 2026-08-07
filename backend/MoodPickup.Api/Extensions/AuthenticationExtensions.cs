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

        services.AddSingleton<IAuthorizationHandler, AccountTypeAuthorizationHandler>();
        services.AddSingleton<IAuthorizationHandler, EmployeePermissionAuthorizationHandler>();
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
                AuthenticationConstants.Roles.OrderReception);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanWorkKitchen,
                AuthenticationConstants.Roles.Kitchen);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanIssueOrders,
                AuthenticationConstants.Roles.Pickup);
            AddEmployeePolicy(
                options,
                AuthenticationConstants.Policies.CanManageMenu,
                AuthenticationConstants.Roles.MenuManager);
            AddEmployeePolicy(options, AuthenticationConstants.Policies.CanManageEmployees);
            AddEmployeePolicy(options, AuthenticationConstants.Policies.CanManageCafeSettings);
            AddEmployeePolicy(options, AuthenticationConstants.Policies.CanViewAuditLog);
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
        params string[] roles)
    {
        options.AddPolicy(
            policyName,
            policy => policy
                .RequireAuthenticatedUser()
                .AddRequirements(new EmployeePermissionRequirement(roles)));
    }
}
