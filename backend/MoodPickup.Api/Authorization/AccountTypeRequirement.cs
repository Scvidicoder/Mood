using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Authorization;

public sealed record AccountTypeRequirement(string AccountType) : IAuthorizationRequirement;

public sealed class AccountTypeAuthorizationHandler(EmployeeAccessStateService accessStateService)
    : AuthorizationHandler<AccountTypeRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccountTypeRequirement requirement)
    {
        if (context.User.FindFirst(AuthenticationConstants.AccountTypeClaim)?.Value !=
            requirement.AccountType)
        {
            return;
        }

        if (requirement.AccountType != AuthenticationConstants.AccountTypes.Employee)
        {
            context.Succeed(requirement);
            return;
        }

        if (!Guid.TryParse(
                context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out var employeeId))
        {
            return;
        }

        var state = await accessStateService.GetAsync(employeeId);
        var sessionVersion = context.User.FindFirst(
            AuthenticationConstants.EmployeeSessionVersionClaim)?.Value;
        if (state?.IsActive == true &&
            Guid.TryParse(sessionVersion, out var tokenSessionVersion) &&
            tokenSessionVersion == state.SessionVersion)
        {
            context.Succeed(requirement);
        }
    }
}
