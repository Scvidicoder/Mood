using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;

namespace MoodPickup.Api.Services;

public sealed class EmployeeAccessStateService(MoodPickupDbContext dbContext)
{
    private readonly Dictionary<Guid, Task<EmployeeAccessState?>> _requests = [];

    public Task<EmployeeAccessState?> GetAsync(
        Guid employeeId,
        CancellationToken cancellationToken = default)
    {
        if (_requests.TryGetValue(employeeId, out var request))
        {
            return request;
        }

        request = dbContext.Employees
            .AsNoTracking()
            .Where(employee => employee.Id == employeeId)
            .Select(employee => new EmployeeAccessState(
                !employee.IsDeleted,
                employee.MustChangePassword,
                employee.SessionVersion,
                employee.EmployeeRoles
                    .Select(employeeRole => employeeRole.Role.Name)
                    .ToArray(),
                employee.PermissionOverrides
                    .Select(permission => new EmployeePermissionState(
                        permission.Permission,
                        permission.IsAllowed))
                    .ToArray()))
            .SingleOrDefaultAsync(cancellationToken);
        _requests.Add(employeeId, request);
        return request;
    }
}

public sealed record EmployeeAccessState(
    bool IsActive,
    bool MustChangePassword,
    Guid SessionVersion,
    IReadOnlyCollection<string> Roles,
    IReadOnlyCollection<EmployeePermissionState> PermissionOverrides);

public sealed record EmployeePermissionState(
    string Permission,
    bool IsAllowed);
