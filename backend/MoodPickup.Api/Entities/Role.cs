namespace MoodPickup.Api.Entities;

public sealed class Role
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = [];
}
