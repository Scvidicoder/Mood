using System.Text.Json;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class EmployeeAuditService(
    MoodPickupDbContext dbContext,
    ICurrentUserContext currentUserContext) : IEmployeeAuditService
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

        dbContext.EmployeeActionLogs.Add(new EmployeeActionLog
        {
            Id = Guid.NewGuid(),
            EmployeeId = currentUserContext.GetRequiredEmployeeId(),
            ActionType = actionType,
            EntityType = entityType,
            EntityId = entityId,
            Description = description,
            OldValuesJson = Serialize(oldValues),
            NewValuesJson = Serialize(newValues),
            CorrelationId = currentUserContext.CorrelationId
        });

        return Task.CompletedTask;
    }

    private static string? Serialize(object? value)
    {
        return value is null ? null : JsonSerializer.Serialize(value, SerializerOptions);
    }
}
