namespace MoodPickup.Api.Entities;

public interface IHasCreatedAt
{
    DateTimeOffset CreatedAt { get; set; }
}

public interface IHasTimestamps : IHasCreatedAt
{
    DateTimeOffset UpdatedAt { get; set; }
}
