namespace MoodPickup.Api.Entities;

public interface IHasNormalizedName
{
    string Name { get; set; }

    string NormalizedName { get; set; }
}
