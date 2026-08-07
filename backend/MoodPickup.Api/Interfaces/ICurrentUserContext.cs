namespace MoodPickup.Api.Interfaces;

public interface ICurrentUserContext
{
    Guid GetRequiredEmployeeId();

    string CorrelationId { get; }
}
