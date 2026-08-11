using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class EmployeeAuthenticationService(
    MoodPickupDbContext dbContext,
    IPasswordHasher<Employee> passwordHasher,
    IPasswordPolicyValidator passwordPolicyValidator,
    ITokenIssuer tokenIssuer,
    RefreshTokenService refreshTokenService,
    TimeProvider timeProvider,
    ILogger<EmployeeAuthenticationService> logger)
{
    public async Task<EmployeeAuthenticationResult> LoginAsync(
        EmployeeLoginRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var username = request.Username.Trim().ToLowerInvariant();
        var employee = await dbContext.Employees
            .Include(existingEmployee => existingEmployee.EmployeeRoles)
            .ThenInclude(employeeRole => employeeRole.Role)
            .SingleOrDefaultAsync(
                existingEmployee => existingEmployee.Username == username,
                cancellationToken);

        if (employee is null || employee.IsDeleted)
        {
            logger.LogWarning("Failed employee login.");
            throw InvalidCredentials();
        }

        var passwordResult = passwordHasher.VerifyHashedPassword(
            employee,
            employee.PasswordHash,
            request.Password);

        if (passwordResult == PasswordVerificationResult.Failed)
        {
            logger.LogWarning("Failed employee login for {Username}.", username);
            throw InvalidCredentials();
        }

        if (passwordResult == PasswordVerificationResult.SuccessRehashNeeded)
        {
            employee.PasswordHash = passwordHasher.HashPassword(employee, request.Password);
        }

        employee.LastLoginAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        var roles = employee.EmployeeRoles
            .Select(employeeRole => employeeRole.Role.Name)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        var accessToken = tokenIssuer.IssueEmployeeAccessToken(employee, roles);
        var refreshToken = await refreshTokenService.IssueAsync(
            AccountType.Employee,
            employee.Id,
            metadata,
            cancellationToken);

        logger.LogInformation(
            "Employee {EmployeeId} logged in successfully.",
            employee.Id);

        return new EmployeeAuthenticationResult(
            accessToken,
            refreshToken,
            employee,
            roles);
    }

    public async Task ChangePasswordAsync(
        Guid employeeId,
        ChangeEmployeePasswordRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await dbContext.Employees
            .SingleOrDefaultAsync(
                existingEmployee =>
                    existingEmployee.Id == employeeId &&
                    !existingEmployee.IsDeleted,
                cancellationToken)
            ?? throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "unauthorized",
                "Authentication required");

        var currentPasswordResult = passwordHasher.VerifyHashedPassword(
            employee,
            employee.PasswordHash,
            request.CurrentPassword);

        if (currentPasswordResult == PasswordVerificationResult.Failed)
        {
            throw new ApiProblemException(
                StatusCodes.Status400BadRequest,
                "invalid_current_password",
                "The current password is incorrect",
                "INVALID_CURRENT_PASSWORD");
        }

        var policyErrors = passwordPolicyValidator.Validate(
            request.NewPassword,
            employee.Username);

        if (policyErrors.Count > 0)
        {
            throw new ApiValidationException(
                new Dictionary<string, string[]>
                {
                    ["newPassword"] = policyErrors.ToArray()
                });
        }

        employee.PasswordHash = passwordHasher.HashPassword(
            employee,
            request.NewPassword);
        employee.MustChangePassword = false;
        employee.UpdatedAt = timeProvider.GetUtcNow();
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Employee {EmployeeId} changed their password.",
            employee.Id);
    }

    private static ApiProblemException InvalidCredentials()
    {
        return new ApiProblemException(
            StatusCodes.Status401Unauthorized,
            "invalid_credentials",
            "Unable to sign in",
            "INVALID_CREDENTIALS");
    }
}

public sealed record EmployeeAuthenticationResult(
    IssuedAccessToken AccessToken,
    IssuedRefreshToken RefreshToken,
    Employee Employee,
    IReadOnlyCollection<string> Roles);
