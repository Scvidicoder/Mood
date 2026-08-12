using System.Diagnostics;
using System.Text.Json;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class SystemAuditService(
    MoodPickupDbContext dbContext,
    IHttpContextAccessor httpContextAccessor) : ISystemAuditService
{
    private static readonly JsonSerializerOptions SerializerOptions =
        new(JsonSerializerDefaults.Web);

    public Task RecordAsync(
        string actionType,
        string entityType,
        Guid entityId,
        string description,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var correlationId = Activity.Current?.TraceId.ToString() ??
            httpContextAccessor.HttpContext?.TraceIdentifier ??
            Guid.NewGuid().ToString("N");

        dbContext.EmployeeActionLogs.Add(new EmployeeActionLog
        {
            Id = Guid.NewGuid(),
            EmployeeId = null,
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            OldValuesJson = Serialize(oldValues),
            NewValuesJson = Serialize(newValues),
            CorrelationId = correlationId
        });

        return Task.CompletedTask;
    }

    private static string? Serialize(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, SerializerOptions);
    }
}
