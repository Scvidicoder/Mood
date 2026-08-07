using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MoodPickup.Api.Infrastructure;

public sealed class FluentValidationActionFilter : IAsyncActionFilter
{
    public async Task OnActionExecutionAsync(
        ActionExecutingContext context,
        ActionExecutionDelegate next)
    {
        var validationErrors = new Dictionary<string, string[]>(StringComparer.Ordinal);

        foreach (var argument in context.ActionArguments.Values.Where(value => value is not null))
        {
            var argumentType = argument!.GetType();
            var validatorType = typeof(IValidator<>).MakeGenericType(argumentType);

            if (context.HttpContext.RequestServices.GetService(validatorType) is not IValidator validator)
            {
                continue;
            }

            var validationContext = new ValidationContext<object>(argument);
            var result = await validator.ValidateAsync(
                validationContext,
                context.HttpContext.RequestAborted);

            foreach (var group in result.Errors.GroupBy(error => ToCamelCase(error.PropertyName)))
            {
                validationErrors[group.Key] = group
                    .Select(error => error.ErrorMessage)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();
            }
        }

        if (validationErrors.Count == 0)
        {
            await next();
            return;
        }

        var problemDetails = new ValidationProblemDetails(validationErrors)
        {
            Type = "validation_error",
            Title = "Request validation failed",
            Status = StatusCodes.Status400BadRequest,
            Instance = context.HttpContext.Request.Path
        };
        problemDetails.Extensions["traceId"] =
            TraceIdProvider.GetTraceId(context.HttpContext);
        problemDetails.Extensions["code"] = "VALIDATION_ERROR";

        context.Result = new BadRequestObjectResult(problemDetails)
        {
            ContentTypes = { "application/problem+json" }
        };
    }

    private static string ToCamelCase(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return string.Empty;
        }

        return char.ToLowerInvariant(propertyName[0]) + propertyName[1..];
    }
}
