using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.Data;
using MoodPickup.Api.DTOs.Audit;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class AuditLogQueryService(MoodPickupDbContext dbContext)
    : IAuditLogQueryService
{
    public async Task<PagedResponse<AuditLogListItemDto>> GetAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken)
    {
        var logs = dbContext.EmployeeActionLogs.AsNoTracking().AsQueryable();

        if (query.EmployeeId is Guid employeeId)
        {
            logs = logs.Where(log => log.EmployeeId == employeeId);
        }

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

        if (query.EntityId is Guid entityId)
        {
            logs = logs.Where(log => log.EntityId == entityId);
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
            .Select(log => new AuditLogListItemDto(
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

        return new PagedResponse<AuditLogListItemDto>(
            items,
            query.Page,
            query.PageSize,
            totalCount,
            MenuServiceSupport.TotalPages(totalCount, query.PageSize));
    }

    public async Task<AuditLogDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken)
    {
        return await dbContext.EmployeeActionLogs
                   .AsNoTracking()
                   .Where(log => log.Id == id)
                   .Select(log => new AuditLogDetailDto(
                       log.Id,
                       log.CreatedAt,
                       log.EmployeeId,
                       log.Employee == null ? "System" : log.Employee.FullName,
                       log.ActionType,
                       log.EntityType,
                       log.EntityId,
                       log.Description,
                       log.OldValuesJson,
                       log.NewValuesJson,
                       log.CorrelationId))
                   .SingleOrDefaultAsync(cancellationToken)
               ?? throw MenuServiceSupport.NotFound(
                   "Audit log entry not found",
                   "AUDIT_LOG_NOT_FOUND");
    }
}
