using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Infrastructure;
using Npgsql;

namespace MoodPickup.Api.Middleware;

public sealed class GlobalExceptionHandler(
    IProblemDetailsService problemDetailsService,
    ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is ApiProblemException apiProblem)
        {
            await WriteApiProblemAsync(
                httpContext,
                apiProblem,
                cancellationToken);
            return true;
        }

        if (exception is DbUpdateConcurrencyException)
        {
            await WriteApiProblemAsync(
                httpContext,
                new ApiProblemException(
                    StatusCodes.Status409Conflict,
                    "concurrency_conflict",
                    "Menu item was changed by another employee",
                    "MENU_VERSION_CONFLICT"),
                cancellationToken);
            return true;
        }

        if (exception is DbUpdateException
            {
                InnerException: PostgresException
                {
                    SqlState: PostgresErrorCodes.UniqueViolation
                } postgresException
            } &&
            TryMapUniqueConstraint(postgresException.ConstraintName, out var uniqueProblem))
        {
            await WriteApiProblemAsync(
                httpContext,
                uniqueProblem,
                cancellationToken);
            return true;
        }

        var traceId = TraceIdProvider.GetTraceId(httpContext);

        logger.LogError(
            exception,
            "Unhandled exception while processing request. TraceId: {TraceId}",
            traceId);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        var problemDetails = new ProblemDetails
        {
            Type = "server_error",
            Title = "An unexpected error occurred",
            Status = StatusCodes.Status500InternalServerError,
            Extensions =
            {
                ["traceId"] = traceId
            }
        };

        await WriteProblemDetailsAsync(
            httpContext,
            problemDetails,
            exception,
            cancellationToken);
        return true;
    }

    private async Task WriteApiProblemAsync(
        HttpContext httpContext,
        ApiProblemException exception,
        CancellationToken cancellationToken)
    {
        httpContext.Response.StatusCode = exception.Status;
        var problemDetails = exception is ApiValidationException validationException
            ? new ValidationProblemDetails(
                validationException.Errors.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value,
                    StringComparer.Ordinal))
            : new ProblemDetails();

        problemDetails.Type = exception.Type;
        problemDetails.Title = exception.Title;
        problemDetails.Status = exception.Status;
        problemDetails.Detail = exception.ProblemDetail;
        problemDetails.Instance = httpContext.Request.Path;
        problemDetails.Extensions["traceId"] = TraceIdProvider.GetTraceId(httpContext);

        if (!string.IsNullOrWhiteSpace(exception.Code))
        {
            problemDetails.Extensions["code"] = exception.Code;
        }

        foreach (var extension in exception.Extensions)
        {
            problemDetails.Extensions[extension.Key] = extension.Value;
        }

        await WriteProblemDetailsAsync(
            httpContext,
            problemDetails,
            exception,
            cancellationToken);
    }

    private async Task WriteProblemDetailsAsync(
        HttpContext httpContext,
        ProblemDetails problemDetails,
        Exception exception,
        CancellationToken cancellationToken)
    {
        var written = await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = problemDetails,
            Exception = exception
        });

        if (!written)
        {
            await httpContext.Response.WriteAsJsonAsync(
                problemDetails,
                cancellationToken);
        }
    }

    private static bool TryMapUniqueConstraint(
        string? constraintName,
        out ApiProblemException problem)
    {
        problem = constraintName switch
        {
            "IX_Customers_TelegramChatId" =>
                new ApiProblemException(
                    StatusCodes.Status409Conflict,
                    "business_rule_violation",
                    "This Telegram identity is already linked to another customer",
                    "TELEGRAM_IDENTITY_CONFLICT"),
            "IX_OptionValues_OptionGroupId_NormalizedName" =>
                new ApiProblemException(
                    StatusCodes.Status409Conflict,
                    "business_rule_violation",
                    "An active option value with this name already exists in the group",
                    "DUPLICATE_OPTION_VALUE_NAME"),
            "IX_ProductOptionGroups_ProductId_OptionGroupId" =>
                new ApiProblemException(
                    StatusCodes.Status409Conflict,
                    "business_rule_violation",
                    "The option group is already assigned to this product",
                    "PRODUCT_OPTION_GROUP_ALREADY_ASSIGNED"),
            "IX_ProductOptionValues_ProductOptionGroupId_OptionValueId" =>
                new ApiProblemException(
                    StatusCodes.Status409Conflict,
                    "business_rule_violation",
                    "The option value is already assigned",
                    "PRODUCT_OPTION_VALUE_ALREADY_ASSIGNED"),
            _ => null!
        };

        return problem is not null;
    }
}
