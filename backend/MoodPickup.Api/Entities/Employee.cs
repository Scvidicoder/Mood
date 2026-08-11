namespace MoodPickup.Api.Entities;

public sealed class Employee : IHasTimestamps, IHasConcurrencyToken
{
    public Guid Id { get; set; }

    public string Username { get; set; } = string.Empty;

    public string PasswordHash { get; set; } = string.Empty;

    public string FullName { get; set; } = string.Empty;

    public bool IsAdmin { get; set; }

    public bool MustChangePassword { get; set; }

    public bool IsDeleted { get; set; }

    public DateTimeOffset? LastLoginAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public Guid RowVersion { get; set; }

    public Guid SessionVersion { get; set; } = Guid.NewGuid();

    public ICollection<EmployeeRole> EmployeeRoles { get; set; } = [];

    public ICollection<EmployeePermission> PermissionOverrides { get; set; } = [];

    public ICollection<RefreshToken> RefreshTokens { get; set; } = [];

    public ICollection<EmployeeActionLog> ActionLogs { get; set; } = [];
}
