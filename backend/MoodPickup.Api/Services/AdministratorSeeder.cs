using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class AdministratorSeeder(
    MoodPickupDbContext dbContext,
    IOptions<AdministratorSeedOptions> options,
    IPasswordHasher<Employee> passwordHasher,
    IPasswordPolicyValidator passwordPolicyValidator,
    TimeProvider timeProvider,
    ILogger<AdministratorSeeder> logger)
{
    private readonly AdministratorSeedOptions _options = options.Value;

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (!_options.Enabled)
        {
            return;
        }

        var rolesByName = await dbContext.Roles
            .ToDictionaryAsync(role => role.Name, StringComparer.Ordinal, cancellationToken);

        foreach (var roleName in AuthenticationConstants.Roles.All)
        {
            if (rolesByName.ContainsKey(roleName))
            {
                continue;
            }

            var role = new Role
            {
                Id = Guid.NewGuid(),
                Name = roleName
            };
            rolesByName.Add(roleName, role);
            dbContext.Roles.Add(role);
        }

        var normalizedUsername = _options.Username.Trim().ToLowerInvariant();
        var existingAdministrator = await dbContext.Employees
            .AnyAsync(
                employee => employee.Username == normalizedUsername,
                cancellationToken);

        if (!existingAdministrator)
        {
            var passwordErrors = passwordPolicyValidator.Validate(
                _options.Password,
                normalizedUsername);

            if (passwordErrors.Count > 0)
            {
                throw new InvalidOperationException(
                    $"The configured initial administrator password is invalid: {string.Join(" ", passwordErrors)}");
            }

            var now = timeProvider.GetUtcNow();
            var administrator = new Employee
            {
                Id = Guid.NewGuid(),
                Username = normalizedUsername,
                FullName = _options.FullName.Trim(),
                IsAdmin = true,
                MustChangePassword = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            administrator.PasswordHash = passwordHasher.HashPassword(
                administrator,
                _options.Password);
            administrator.EmployeeRoles.Add(new EmployeeRole
            {
                Employee = administrator,
                Role = rolesByName[AuthenticationConstants.Roles.Administrator]
            });

            dbContext.Employees.Add(administrator);
            logger.LogInformation(
                "Created the configured initial administrator account {EmployeeId}.",
                administrator.Id);
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
