using Microsoft.AspNetCore.Http.Features;
using MoodPickup.Api.Interfaces;
using MoodPickup.Api.Options;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Extensions;

public static class MenuExtensions
{
    public static IServiceCollection AddMenuDomain(
        this IServiceCollection services,
        IConfiguration configuration,
        IHostEnvironment environment)
    {
        var mediaOptions = configuration
            .GetRequiredSection(MediaStorageOptions.SectionName)
            .Get<MediaStorageOptions>()
            ?? throw new InvalidOperationException(
                "MediaStorage configuration is required.");
        services.Configure<FormOptions>(options =>
            options.MultipartBodyLengthLimit =
                mediaOptions.MaximumFileSizeBytes + 1024 * 1024);
        services.AddSingleton<IMenuConfigurationValidator, MenuConfigurationValidator>();
        services.AddSingleton<IMediaStorage, LocalMediaStorage>();
        services.AddScoped<IMediaService, MediaService>();
        services.AddScoped<IDevelopmentMenuSeeder, DevelopmentMenuSeeder>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserContext, CurrentUserContext>();
        services.AddScoped<IEmployeeAuditService, EmployeeAuditService>();
        services.AddScoped<ISystemAuditService, SystemAuditService>();
        services.AddScoped<IPublicMenuService, PublicMenuService>();
        services.AddScoped<IAdminCategoryService, AdminCategoryService>();
        services.AddScoped<IAdminProductService, AdminProductService>();
        services.AddScoped<IAdminOptionService, AdminOptionService>();
        services.AddScoped<IAdminProductConfigurationService, AdminProductConfigurationService>();
        services.AddScoped<IAuditLogQueryService, AuditLogQueryService>();
        services.AddSingleton<TemporaryPasswordGenerator>();
        services.AddScoped<IEmployeeManagementService, EmployeeManagementService>();
        services.AddScoped<IOrderService, OrderService>();
        services.AddScoped<ICustomerProfileService, CustomerProfileService>();
        services.AddScoped<IStaffOrderService, StaffOrderService>();
        services.AddScoped<IOrderWorkflowService, OrderWorkflowService>();
        services.AddSingleton<AlifSignatureService>();
        services.AddHttpClient<AlifPaymentClient>();
        services.AddScoped<IPaymentProvider, AlifPaymentProvider>();
        if (environment.IsDevelopment())
        {
            services.AddScoped<IPaymentProvider, DevelopmentPaymentProvider>();
        }
        services.AddScoped<IPaymentService, PaymentService>();
        services.AddSingleton<IOrderRealtimeNotifier, SignalROrderRealtimeNotifier>();
        return services;
    }

    public static async Task SeedDevelopmentMenuAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();
        var seeder = scope.ServiceProvider.GetRequiredService<IDevelopmentMenuSeeder>();
        await seeder.SeedAsync(CancellationToken.None);
    }
}
