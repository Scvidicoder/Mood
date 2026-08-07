using System.Reflection;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace MoodPickup.Api.Extensions;

public static class SwaggerExtensions
{
    public static IServiceCollection AddSwaggerDocumentation(
        this IServiceCollection services)
    {
        services.AddEndpointsApiExplorer();
        services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo
            {
                Title = "Mood Pickup API",
                Version = "v1",
                Description =
                    "Mood Pickup API with secure authentication, public menu browsing, " +
                    "authorized menu administration, GUID concurrency, and employee audit logs."
            });

            var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
            var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);

            if (File.Exists(xmlPath))
            {
                options.IncludeXmlComments(xmlPath);
            }
        });

        return services;
    }

    public static WebApplication UseConfiguredSwagger(this WebApplication app)
    {
        var swaggerOptions = app.Services
            .GetRequiredService<IOptions<SwaggerOptions>>()
            .Value;

        if (app.Environment.IsDevelopment() || swaggerOptions.EnabledInProduction)
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/swagger/v1/swagger.json", "Mood Pickup API v1");
                options.DocumentTitle = "Mood Pickup API";
            });
        }

        return app;
    }
}
