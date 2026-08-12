using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class EmployeeManagementService(
    MoodPickupDbContext dbContext,
    IPasswordHasher<Employee> passwordHasher,
    TemporaryPasswordGenerator temporaryPasswordGenerator,
    RefreshTokenService refreshTokenService,
    IEmployeeAuditService auditService,
    TimeProvider timeProvider) : IEmployeeManagementService
{
    private const string EmployeeEntityType = "Employee";

    public async Task<PagedResponse<EmployeeListItemDto>> GetAsync(
        EmployeeListQuery query,
        CancellationToken cancellationToken)
    {
        var employees = dbContext.Employees.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(query.Search))
        {
            var pattern = $"%{query.Search.Trim()}%";
            employees = employees.Where(employee =>
                EF.Functions.ILike(employee.FullName, pattern) ||
                EF.Functions.ILike(employee.Username, pattern));
        }

        if (!string.IsNullOrWhiteSpace(query.Role))
        {
            var role = query.Role.Trim();
            var normalizedRole = role.ToLowerInvariant();
            employees = employees.Where(employee => employee.EmployeeRoles.Any(
                employeeRole => employeeRole.Role.Name.ToLower() == normalizedRole));
        }

        employees = query.Status switch
        {
            EmployeeStatusFilter.Active => employees.Where(employee => !employee.IsDeleted),
            EmployeeStatusFilter.Disabled => employees.Where(employee => employee.IsDeleted),
            _ => employees
        };

        var totalCount = await employees.CountAsync(cancellationToken);
        var page = await employees
            .OrderBy(employee => employee.FullName)
            .ThenBy(employee => employee.Username)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(employee => new
            {
                employee.Id,
                employee.FullName,
                employee.Username,
                Roles = employee.EmployeeRoles
                    .Select(employeeRole => employeeRole.Role.Name)
                    .OrderBy(role => role)
                    .ToList(),
                employee.IsDeleted,
                employee.MustChangePassword,
                employee.CreatedAt,
                employee.UpdatedAt,
                employee.LastLoginAt,
                employee.RowVersion
            })
            .ToListAsync(cancellationToken);

        return new PagedResponse<EmployeeListItemDto>(
            page.Select(employee => new EmployeeListItemDto(
                employee.Id,
                employee.FullName,
                employee.Username,
                employee.Roles,
                !employee.IsDeleted,
                employee.MustChangePassword,
                employee.CreatedAt,
                employee.UpdatedAt,
                employee.LastLoginAt,
                employee.RowVersion)).ToList(),
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<EmployeeDetailsDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var employee = await LoadEmployeeAsync(id, asTracking: false, cancellationToken);
        return Map(employee);
    }

    public async Task<IReadOnlyList<RoleOptionDto>> GetRolesAsync(
        CancellationToken cancellationToken)
    {
        return await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .Select(role => new RoleOptionDto(role.Name, DisplayRole(role.Name)))
            .ToListAsync(cancellationToken);
    }

    public async Task<EmployeePermissionsResponse> GetPermissionsAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        var employee = await LoadEmployeeAsync(id, asTracking: false, cancellationToken);
        return MapPermissions(employee);
    }

    public async Task<EmployeePermissionsResponse> ReplacePermissionOverridesAsync(
        Guid id,
        ReplaceEmployeePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        var employee = await LoadEmployeeAsync(id, asTracking: true, cancellationToken);
        var replacements = ValidatePermissionOverrides(request.Overrides);
        var previous = employee.PermissionOverrides
            .OrderBy(permission => permission.Permission, StringComparer.Ordinal)
            .Select(permission => new
            {
                permission.Permission,
                permission.IsAllowed
            })
            .ToArray();

        var replacementsByPermission = replacements.ToDictionary(
            permission => permission.Permission,
            StringComparer.Ordinal);
        var existingByPermission = employee.PermissionOverrides.ToDictionary(
            permission => permission.Permission,
            StringComparer.Ordinal);
        var removed = employee.PermissionOverrides
            .Where(permission =>
                !replacementsByPermission.ContainsKey(permission.Permission))
            .ToArray();
        dbContext.EmployeePermissions.RemoveRange(removed);
        foreach (var permission in removed)
        {
            employee.PermissionOverrides.Remove(permission);
        }

        foreach (var replacement in replacements)
        {
            if (existingByPermission.TryGetValue(
                    replacement.Permission,
                    out var existing))
            {
                existing.IsAllowed = replacement.IsAllowed;
            }
            else
            {
                employee.PermissionOverrides.Add(new EmployeePermission
                {
                    EmployeeId = employee.Id,
                    Employee = employee,
                    Permission = replacement.Permission,
                    IsAllowed = replacement.IsAllowed
                });
            }
        }

        await auditService.RecordAsync(
            "EmployeePermissionsUpdated",
            EmployeeEntityType,
            employee.Id,
            $"Updated permissions for employee {employee.Username}.",
            new { overrides = previous },
            new
            {
                overrides = replacements.Select(permission => new
                {
                    permission.Permission,
                    permission.IsAllowed
                })
            },
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapPermissions(employee);
    }

    public async Task<CreateEmployeeResponse> CreateAsync(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var username = NormalizeUsername(request.Username);
        await EnsureUsernameAvailableAsync(username, null, cancellationToken);
        var roles = await ResolveRolesAsync(request.Roles, cancellationToken);
        var temporaryPassword = temporaryPasswordGenerator.Generate();
        var employee = new Employee
        {
            Id = Guid.NewGuid(),
            FullName = request.FullName.Trim(),
            Username = username,
            MustChangePassword = true,
            IsAdmin = roles.Any(role => role.Name == AuthenticationConstants.Roles.Administrator)
        };
        employee.PasswordHash = passwordHasher.HashPassword(employee, temporaryPassword);

        foreach (var role in roles)
        {
            employee.EmployeeRoles.Add(new EmployeeRole
            {
                Employee = employee,
                Role = role
            });
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        dbContext.Employees.Add(employee);
        await auditService.RecordAsync(
            "EmployeeCreated",
            EmployeeEntityType,
            employee.Id,
            $"Created employee {employee.Username}.",
            null,
            SafeState(employee, roles.Select(role => role.Name)),
            cancellationToken);
        await SaveMutationAsync(employee, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new CreateEmployeeResponse(Map(employee), temporaryPassword);
    }

    public async Task<EmployeeDetailsDto> UpdateAsync(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var employee = await LoadEmployeeAsync(id, asTracking: true, cancellationToken);
        EnsureVersion(employee, request.RowVersion);
        var username = NormalizeUsername(request.Username);
        await EnsureUsernameAvailableAsync(username, id, cancellationToken);
        var roles = await ResolveRolesAsync(request.Roles, cancellationToken);
        var oldRoles = employee.EmployeeRoles
            .Select(employeeRole => employeeRole.Role.Name)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
        var newRoles = roles
            .Select(role => role.Name)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();

        await ProtectLastAdministratorAsync(
            employee,
            newRoles.Contains(AuthenticationConstants.Roles.Administrator),
            remainsActive: !employee.IsDeleted,
            cancellationToken);

        var oldState = SafeState(employee, oldRoles);
        employee.FullName = request.FullName.Trim();
        employee.Username = username;
        employee.IsAdmin = newRoles.Contains(AuthenticationConstants.Roles.Administrator);
        employee.UpdatedAt = timeProvider.GetUtcNow();
        var requestedRoleIds = roles.Select(role => role.Id).ToHashSet();
        var removedRoles = employee.EmployeeRoles
            .Where(employeeRole => !requestedRoleIds.Contains(employeeRole.RoleId))
            .ToList();
        dbContext.EmployeeRoles.RemoveRange(removedRoles);
        foreach (var removedRole in removedRoles)
        {
            employee.EmployeeRoles.Remove(removedRole);
        }

        var existingRoleIds = employee.EmployeeRoles
            .Select(employeeRole => employeeRole.RoleId)
            .ToHashSet();
        foreach (var role in roles.Where(role => !existingRoleIds.Contains(role.Id)))
        {
            employee.EmployeeRoles.Add(new EmployeeRole
            {
                EmployeeId = employee.Id,
                Employee = employee,
                RoleId = role.Id,
                Role = role
            });
        }

        await auditService.RecordAsync(
            "EmployeeUpdated",
            EmployeeEntityType,
            employee.Id,
            $"Updated employee {employee.Username}.",
            oldState,
            SafeState(employee, newRoles),
            cancellationToken);
        if (!oldRoles.SequenceEqual(newRoles, StringComparer.Ordinal))
        {
            await auditService.RecordAsync(
                "EmployeeRolesChanged",
                EmployeeEntityType,
                employee.Id,
                $"Changed roles for employee {employee.Username}.",
                new { roles = oldRoles },
                new { roles = newRoles },
                cancellationToken);
        }

        await SaveMutationAsync(employee, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(employee);
    }

    public async Task<EmployeeDetailsDto> DisableAsync(
        Guid id,
        EmployeeVersionRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var employee = await LoadEmployeeAsync(id, asTracking: true, cancellationToken);
        EnsureVersion(employee, request.RowVersion);
        if (employee.IsDeleted)
        {
            throw Conflict(
                "Employee is already disabled",
                "EMPLOYEE_ALREADY_DISABLED");
        }

        await ProtectLastAdministratorAsync(
            employee,
            employee.EmployeeRoles.Any(employeeRole =>
                employeeRole.Role.Name == AuthenticationConstants.Roles.Administrator),
            remainsActive: false,
            cancellationToken);
        var oldState = SafeState(employee, CurrentRoles(employee));
        employee.IsDeleted = true;
        employee.SessionVersion = Guid.NewGuid();
        employee.UpdatedAt = timeProvider.GetUtcNow();
        var revokedSessions = await refreshTokenService.MarkEmployeeSessionsRevokedAsync(
            employee.Id,
            metadata,
            cancellationToken);
        await auditService.RecordAsync(
            "EmployeeDisabled",
            EmployeeEntityType,
            employee.Id,
            $"Disabled employee {employee.Username} and revoked {revokedSessions} active session(s).",
            oldState,
            SafeState(employee, CurrentRoles(employee)),
            cancellationToken);
        await SaveMutationAsync(employee, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(employee);
    }

    public async Task<EmployeeDetailsDto> EnableAsync(
        Guid id,
        EmployeeVersionRequest request,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var employee = await LoadEmployeeAsync(id, asTracking: true, cancellationToken);
        EnsureVersion(employee, request.RowVersion);
        if (!employee.IsDeleted)
        {
            throw Conflict("Employee is already enabled", "EMPLOYEE_ALREADY_ENABLED");
        }

        var oldState = SafeState(employee, CurrentRoles(employee));
        employee.IsDeleted = false;
        employee.UpdatedAt = timeProvider.GetUtcNow();
        await auditService.RecordAsync(
            "EmployeeEnabled",
            EmployeeEntityType,
            employee.Id,
            $"Enabled employee {employee.Username}.",
            oldState,
            SafeState(employee, CurrentRoles(employee)),
            cancellationToken);
        await SaveMutationAsync(employee, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Map(employee);
    }

    public async Task<ResetEmployeePasswordResponse> ResetPasswordAsync(
        Guid id,
        EmployeeVersionRequest request,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);
        var employee = await LoadEmployeeAsync(id, asTracking: true, cancellationToken);
        EnsureVersion(employee, request.RowVersion);
        var temporaryPassword = temporaryPasswordGenerator.Generate();
        var previousMustChangePassword = employee.MustChangePassword;
        employee.PasswordHash = passwordHasher.HashPassword(employee, temporaryPassword);
        employee.MustChangePassword = true;
        employee.SessionVersion = Guid.NewGuid();
        employee.UpdatedAt = timeProvider.GetUtcNow();
        var revokedSessions = await refreshTokenService.MarkEmployeeSessionsRevokedAsync(
            employee.Id,
            metadata,
            cancellationToken);
        await auditService.RecordAsync(
            "EmployeePasswordReset",
            EmployeeEntityType,
            employee.Id,
            $"Reset password for employee {employee.Username} and revoked {revokedSessions} active session(s).",
            new { mustChangePassword = previousMustChangePassword },
            new { mustChangePassword = true, revokedSessionCount = revokedSessions },
            cancellationToken);
        await SaveMutationAsync(employee, cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new ResetEmployeePasswordResponse(
            temporaryPassword,
            employee.MustChangePassword,
            employee.RowVersion,
            revokedSessions);
    }

    public async Task<PagedResponse<EmployeeActionListItemDto>> GetActionsAsync(
        Guid id,
        EmployeeActionQuery query,
        CancellationToken cancellationToken)
    {
        if (!await dbContext.Employees
                .AsNoTracking()
                .AnyAsync(employee => employee.Id == id, cancellationToken))
        {
            throw NotFound();
        }

        var logs = dbContext.EmployeeActionLogs
            .AsNoTracking()
            .Where(log =>
                log.EmployeeId == id ||
                (log.EntityType == EmployeeEntityType && log.EntityId == id));
        if (!string.IsNullOrWhiteSpace(query.ActionType))
        {
            var actionType = query.ActionType.Trim();
            logs = logs.Where(log => log.ActionType == actionType);
        }

        if (!string.IsNullOrWhiteSpace(query.EntityType))
        {
            var entityType = query.EntityType.Trim();
            logs = logs.Where(log => log.EntityType == entityType);
        }

        if (query.DateFrom is DateTimeOffset dateFrom)
        {
            logs = logs.Where(log => log.CreatedAt >= dateFrom);
        }

        if (query.DateTo is DateTimeOffset dateTo)
        {
            logs = logs.Where(log => log.CreatedAt <= dateTo);
        }

        var totalCount = await logs.CountAsync(cancellationToken);
        var items = await logs
            .OrderByDescending(log => log.CreatedAt)
            .ThenByDescending(log => log.Id)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(log => new EmployeeActionListItemDto(
                log.Id,
                log.CreatedAt,
                log.EmployeeId,
                log.Employee == null ? "System" : log.Employee.FullName,
                log.ActionType,
                log.EntityType,
                log.EntityId,
                log.Description,
                log.CorrelationId))
            .ToListAsync(cancellationToken);

        return new PagedResponse<EmployeeActionListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    private async Task<Employee> LoadEmployeeAsync(
        Guid id,
        bool asTracking,
        CancellationToken cancellationToken)
    {
        IQueryable<Employee> query = dbContext.Employees;
        if (!asTracking)
        {
            query = query.AsNoTracking();
        }

        return await query
                   .Include(employee => employee.EmployeeRoles)
                   .ThenInclude(employeeRole => employeeRole.Role)
                   .Include(employee => employee.PermissionOverrides)
                   .SingleOrDefaultAsync(employee => employee.Id == id, cancellationToken)
               ?? throw NotFound();
    }

    private static IReadOnlyList<EmployeePermissionOverrideRequest>
        ValidatePermissionOverrides(
            IReadOnlyList<EmployeePermissionOverrideRequest>? overrides)
    {
        var replacements = overrides ?? [];
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var replacement in replacements)
        {
            if (!EmployeePermissionCatalog.IsKnown(replacement.Permission) ||
                !seen.Add(replacement.Permission))
            {
                throw new ApiProblemException(
                    StatusCodes.Status400BadRequest,
                    "validation_error",
                    "One or more employee permission overrides are invalid",
                    "INVALID_EMPLOYEE_PERMISSION");
            }
        }

        return replacements
            .OrderBy(permission => permission.Permission, StringComparer.Ordinal)
            .ToArray();
    }

    private async Task<IReadOnlyList<Role>> ResolveRolesAsync(
        IReadOnlyList<string> requestedRoles,
        CancellationToken cancellationToken)
    {
        var normalized = requestedRoles
            .Select(role => role.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var availableRoles = await dbContext.Roles.ToListAsync(cancellationToken);
        var availableByName = availableRoles.ToDictionary(
            role => role.Name,
            StringComparer.OrdinalIgnoreCase);
        var roles = normalized
            .Where(availableByName.ContainsKey)
            .Select(role => availableByName[role])
            .ToList();
        if (roles.Count != normalized.Count)
        {
            var known = roles.Select(role => role.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var invalid = normalized.Where(role => !known.Contains(role)).OrderBy(role => role);
            throw new ApiProblemException(
                StatusCodes.Status400BadRequest,
                "validation_error",
                "One or more employee roles are invalid",
                "INVALID_EMPLOYEE_ROLE",
                $"Unknown roles: {string.Join(", ", invalid)}.");
        }

        return roles;
    }

    private async Task EnsureUsernameAvailableAsync(
        string username,
        Guid? employeeId,
        CancellationToken cancellationToken)
    {
        if (await dbContext.Employees.AnyAsync(
                employee => employee.Username == username && employee.Id != employeeId,
                cancellationToken))
        {
            throw Conflict(
                "An employee with this username already exists",
                "EMPLOYEE_USERNAME_CONFLICT");
        }
    }

    private async Task ProtectLastAdministratorAsync(
        Employee employee,
        bool remainsAdministrator,
        bool remainsActive,
        CancellationToken cancellationToken)
    {
        var currentlyActiveAdministrator =
            !employee.IsDeleted && employee.EmployeeRoles.Any(employeeRole =>
                employeeRole.Role.Name == AuthenticationConstants.Roles.Administrator);
        if (!currentlyActiveAdministrator || (remainsAdministrator && remainsActive))
        {
            return;
        }

        var anotherActiveAdministratorExists = await dbContext.Employees
            .AnyAsync(
                candidate =>
                    candidate.Id != employee.Id &&
                    !candidate.IsDeleted &&
                    candidate.EmployeeRoles.Any(employeeRole =>
                        employeeRole.Role.Name == AuthenticationConstants.Roles.Administrator),
                cancellationToken);
        if (!anotherActiveAdministratorExists)
        {
            throw Conflict(
                "At least one active Administrator account must remain",
                "LAST_ADMINISTRATOR_PROTECTION");
        }
    }

    private async Task SaveMutationAsync(
        Employee employee,
        CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw VersionConflict(employee);
        }
    }

    private static void EnsureVersion(Employee employee, Guid expected)
    {
        if (employee.RowVersion != expected)
        {
            throw VersionConflict(employee);
        }
    }

    private static EmployeeDetailsDto Map(Employee employee)
    {
        return new EmployeeDetailsDto(
            employee.Id,
            employee.FullName,
            employee.Username,
            CurrentRoles(employee),
            !employee.IsDeleted,
            employee.MustChangePassword,
            employee.CreatedAt,
            employee.UpdatedAt,
            employee.LastLoginAt,
            employee.RowVersion);
    }

    private static EmployeePermissionsResponse MapPermissions(Employee employee)
    {
        var roles = CurrentRoles(employee);
        var overrides = employee.PermissionOverrides.ToDictionary(
            permission => permission.Permission,
            permission => permission.IsAllowed,
            StringComparer.Ordinal);
        var permissions = EmployeePermissionCatalog.All
            .Select(definition =>
            {
                var roleAllowed = EmployeePermissionCatalog.IsAllowedByRoles(
                    definition,
                    roles);
                var hasOverride = overrides.TryGetValue(
                    definition.Permission,
                    out var overrideValue);
                return new EmployeePermissionDto(
                    definition.Permission,
                    definition.DisplayName,
                    definition.Group,
                    roleAllowed,
                    hasOverride ? overrideValue : null,
                    hasOverride ? overrideValue : roleAllowed);
            })
            .ToArray();
        return new EmployeePermissionsResponse(employee.Id, permissions);
    }

    private static string[] CurrentRoles(Employee employee)
    {
        return employee.EmployeeRoles
            .Select(employeeRole => employeeRole.Role.Name)
            .OrderBy(role => role, StringComparer.Ordinal)
            .ToArray();
    }

    private static object SafeState(Employee employee, IEnumerable<string> roles)
    {
        return new
        {
            employee.FullName,
            employee.Username,
            Roles = roles.OrderBy(role => role, StringComparer.Ordinal).ToArray(),
            IsActive = !employee.IsDeleted,
            employee.MustChangePassword
        };
    }

    private static string NormalizeUsername(string username)
    {
        return username.Trim().ToLowerInvariant();
    }

    private static string DisplayRole(string role)
    {
        return role switch
        {
            AuthenticationConstants.Roles.MenuManager => "Menu Manager",
            AuthenticationConstants.Roles.OrderReception => "Order Reception",
            _ => role
        };
    }

    private static ApiProblemException NotFound()
    {
        return new ApiProblemException(
            StatusCodes.Status404NotFound,
            "not_found",
            "Employee not found",
            "EMPLOYEE_NOT_FOUND");
    }

    private static ApiProblemException Conflict(string title, string code)
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "business_rule_violation",
            title,
            code);
    }

    private static ApiProblemException VersionConflict(Employee employee)
    {
        return new ApiProblemException(
            StatusCodes.Status409Conflict,
            "concurrency_conflict",
            "Employee was changed by another administrator",
            "EMPLOYEE_VERSION_CONFLICT",
            extensions: new Dictionary<string, object?>
            {
                ["currentResource"] = new
                {
                    employee.Id,
                    employee.RowVersion
                }
            });
    }
}
