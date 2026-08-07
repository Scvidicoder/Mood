using Microsoft.AspNetCore.Authorization;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Authorization;

public sealed record AccountTypeRequirement(string AccountType) : IAuthorizationRequirement;

public sealed class AccountTypeAuthorizationHandler
    : AuthorizationHandler<AccountTypeRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccountTypeRequirement requirement)
    {
        if (context.User.FindFirst(AuthenticationConstants.AccountTypeClaim)?.Value ==
            requirement.AccountType)
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
