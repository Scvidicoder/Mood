namespace MoodPickup.Api.Interfaces;

public interface IEmployeeAuditService
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
