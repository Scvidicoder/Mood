using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Authorization;

public sealed record EmployeePermissionRequirement(
    string Permission,
    params string[] AllowedRoles)
    : IAuthorizationRequirement;

public sealed class EmployeePermissionAuthorizationHandler(
    EmployeeAccessStateService accessStateService)
    : AuthorizationHandler<EmployeePermissionRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        EmployeePermissionRequirement requirement)
    {
        var isEmployee =
            context.User.FindFirst(AuthenticationConstants.AccountTypeClaim)?.Value ==
            AuthenticationConstants.AccountTypes.Employee;
        if (!isEmployee ||
            !Guid.TryParse(
                context.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value,
                out var employeeId))
        {
            return;
        }

        var state = await accessStateService.GetAsync(employeeId);
        var sessionVersion = context.User.FindFirst(
            AuthenticationConstants.EmployeeSessionVersionClaim)?.Value;
        if (state is null ||
            !state.IsActive ||
            state.MustChangePassword ||
            !Guid.TryParse(sessionVersion, out var tokenSessionVersion) ||
            tokenSessionVersion != state.SessionVersion)
        {
            return;
        }

        var permissionOverride = state.PermissionOverrides.FirstOrDefault(
            permission => permission.Permission == requirement.Permission);
        if (permissionOverride is not null)
        {
            if (permissionOverride.IsAllowed)
            {
                context.Succeed(requirement);
            }

            return;
        }

        if (state.Roles.Contains(
                AuthenticationConstants.Roles.Administrator,
                StringComparer.Ordinal) ||
            requirement.AllowedRoles.Any(role => state.Roles.Contains(
                role,
                StringComparer.Ordinal)))
        {
            context.Succeed(requirement);
        }
    }
}
