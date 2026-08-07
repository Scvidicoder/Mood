namespace MoodPickup.Api.Entities;

public interface IHasConcurrencyToken
{
    Guid RowVersion { get; set; }
}
