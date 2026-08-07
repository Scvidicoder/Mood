using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Hubs;

[Authorize]
public sealed class NotificationsHub : Hub
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
            await Groups.AddToGroupAsync(
                Context.ConnectionId,
                "staff:all",
                Context.ConnectionAborted);

            foreach (var role in user!.FindAll("roles").Select(claim => claim.Value))
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
