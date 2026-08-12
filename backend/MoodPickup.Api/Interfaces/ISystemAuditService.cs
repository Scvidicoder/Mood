namespace MoodPickup.Api.Interfaces;

public interface ISystemAuditService
{
    Task RecordAsync(
        string actionType,
        string entityType,
        Guid entityId,
        string description,
        object? oldValues,
        object? newValues,
        CancellationToken cancellationToken);
}
