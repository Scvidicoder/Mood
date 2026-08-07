using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Extensions;

public static class ApiErrorExtensions
{
    public static IServiceCollection ConfigureApiErrorResponses(
        this IServiceCollection services)
    {
        services.Configure<ApiBehaviorOptions>(options =>
        {
            options.InvalidModelStateResponseFactory = context =>
            {
                var problemDetails = new ValidationProblemDetails(context.ModelState)
                {
                    Type = "validation_error",
                    Title = "Request validation failed",
                    Status = StatusCodes.Status400BadRequest,
                    Instance = context.HttpContext.Request.Path
                };

                problemDetails.Extensions["traceId"] =
                    TraceIdProvider.GetTraceId(context.HttpContext);
                problemDetails.Extensions["code"] = "VALIDATION_ERROR";

                return new BadRequestObjectResult(problemDetails)
                {
                    ContentTypes = { "application/problem+json" }
                };
            };
        });

        return services;
    }
}
