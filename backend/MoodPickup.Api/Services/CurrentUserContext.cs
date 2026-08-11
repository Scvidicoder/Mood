using System.IdentityModel.Tokens.Jwt;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class CurrentUserContext(IHttpContextAccessor httpContextAccessor)
    : ICurrentUserContext
{
    public string CorrelationId
    {
        get
        {
            var context = httpContextAccessor.HttpContext;
            return context is null
                ? string.Empty
                : TraceIdProvider.GetTraceId(context);
        }
    }

    public Guid GetRequiredEmployeeId()
    {
        var subject = httpContextAccessor.HttpContext?.User
            .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(subject, out var employeeId))
        {
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Authentication required");
        }

        return employeeId;
    }

    public Guid GetRequiredCustomerId()
    {
        var subject = httpContextAccessor.HttpContext?.User
            .FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

        if (!Guid.TryParse(subject, out var customerId))
        {
            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Authentication required");
        }

        return customerId;
    }
}
