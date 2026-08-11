namespace MoodPickup.Api.Entities;

public sealed class EmployeePermission
{
    public Guid EmployeeId { get; set; }

    public Employee Employee { get; set; } = null!;

    public string Permission { get; set; } = string.Empty;

    public bool IsAllowed { get; set; }
}
