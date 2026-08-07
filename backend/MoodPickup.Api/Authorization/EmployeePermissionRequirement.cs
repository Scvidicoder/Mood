using Microsoft.AspNetCore.Authorization;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Authorization;

public sealed record EmployeePermissionRequirement(params string[] AllowedRoles)
    : IAuthorizationRequirement;

public sealed class EmployeePermissionAuthorizationHandler
    : AuthorizationHandler<EmployeePermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmployeePermissionRequirement requirement)
    {
        var isEmployee =
            context.User.FindFirst(AuthenticationConstants.AccountTypeClaim)?.Value ==
            AuthenticationConstants.AccountTypes.Employee;
        var mustChangePassword =
            string.Equals(
                context.User.FindFirst(AuthenticationConstants.MustChangePasswordClaim)?.Value,
                bool.TrueString,
                StringComparison.OrdinalIgnoreCase);

        if (!isEmployee || mustChangePassword)
        {
            return Task.CompletedTask;
        }

        if (context.User.IsInRole(AuthenticationConstants.Roles.Administrator) ||
            requirement.AllowedRoles.Any(context.User.IsInRole))
        {
            context.Succeed(requirement);
        }

        return Task.CompletedTask;
    }
}
