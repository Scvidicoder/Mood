using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class SessionService(
    MoodPickupDbContext dbContext,
    RefreshTokenService refreshTokenService,
    ITokenIssuer tokenIssuer)
{
    public async Task<RefreshedSession> RefreshAsync(
        string rawRefreshToken,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var rotatedToken = await refreshTokenService.RotateAsync(
            rawRefreshToken,
            metadata,
            cancellationToken);

        if (rotatedToken.AccountType == AccountType.Customer)
        {
            var customer = await dbContext.Customers
                .SingleOrDefaultAsync(
                    existingCustomer => existingCustomer.Id == rotatedToken.AccountId,
                    cancellationToken)
                ?? throw InvalidSession();
            var accessToken = tokenIssuer.IssueCustomerAccessToken(customer);

            return new RefreshedSession(
                accessToken,
                rotatedToken.RawToken,
                rotatedToken.ExpiresAt);
        }

        var employee = await dbContext.Employees
            .Include(existingEmployee => existingEmployee.EmployeeRoles)
            .ThenInclude(employeeRole => employeeRole.Role)
            .SingleOrDefaultAsync(
                existingEmployee =>
                    existingEmployee.Id == rotatedToken.AccountId &&
                    !existingEmployee.IsDeleted,
                cancellationToken)
            ?? throw InvalidSession();
        var roles = employee.EmployeeRoles
            .Select(employeeRole => employeeRole.Role.Name)
            .ToArray();
        var employeeAccessToken = tokenIssuer.IssueEmployeeAccessToken(employee, roles);

        return new RefreshedSession(
            employeeAccessToken,
            rotatedToken.RawToken,
            rotatedToken.ExpiresAt);
    }

    public Task LogoutAsync(
        string? rawRefreshToken,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        return refreshTokenService.RevokeAsync(
            rawRefreshToken,
            metadata,
            cancellationToken);
    }

    private static ApiProblemException InvalidSession()
    {
        return new ApiProblemException(
            StatusCodes.Status401Unauthorized,
            "invalid_refresh_token",
            "The session is invalid or expired",
            "INVALID_REFRESH_TOKEN");
    }
}

public sealed record RefreshedSession(
    IssuedAccessToken AccessToken,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);
