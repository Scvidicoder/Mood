namespace MoodPickup.Api.Interfaces;

public interface ICurrentUserContext
{
    Guid GetRequiredCustomerId();

    Guid GetRequiredEmployeeId();

    string CorrelationId { get; }
}
