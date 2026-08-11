using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Services;

namespace MoodPickup.Api.Hubs;

[Authorize]
public sealed class NotificationsHub(EmployeeAccessStateService employeeAccessStateService)
    : Hub
{
    public override async Task OnConnectedAsync()
    {
        var user = Context.User;
        var accountId = user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
        var accountType = user?.FindFirst(AuthenticationConstants.AccountTypeClaim)?.Value;

        if (string.IsNullOrWhiteSpace(accountId))
        {
            Context.Abort();
            return;
        }

        if (accountType == AuthenticationConstants.AccountTypes.Customer)
        {
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                $"customer:{accountId}",
                Context.ConnectionAborted);
        }
        else if (accountType == AuthenticationConstants.AccountTypes.Employee)
        {
            if (!Guid.TryParse(accountId, out var employeeId))
            {
                Context.Abort();
                return;
            }

            var state = await employeeAccessStateService.GetAsync(
                employeeId,
                Context.ConnectionAborted);
            var sessionVersion = user?.FindFirst(
                AuthenticationConstants.EmployeeSessionVersionClaim)?.Value;
            if (state is null ||
                !state.IsActive ||
                state.MustChangePassword ||
                !Guid.TryParse(sessionVersion, out var tokenSessionVersion) ||
                tokenSessionVersion != state.SessionVersion)
            {
                Context.Abort();
                return;
            }

            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                "staff:all",
                Context.ConnectionAborted);

            foreach (var role in state.Roles)
            {
                await Groups.AddToGroupAsync(
                    Context.ConnectionId,
                    $"role:{role}",
                    Context.ConnectionAborted);
            }
        }

        await base.OnConnectedAsync();
    }
}
