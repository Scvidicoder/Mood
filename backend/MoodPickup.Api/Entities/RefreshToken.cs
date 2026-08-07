namespace MoodPickup.Api.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; }

    public Guid FamilyId { get; set; }

    public AccountType AccountType { get; set; }

    public Guid? CustomerId { get; set; }

    public Customer? Customer { get; set; }

    public Guid? EmployeeId { get; set; }

    public Employee? Employee { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? RevokedAt { get; set; }

    public Guid? ReplacedByTokenId { get; set; }

    public RefreshToken? ReplacedByToken { get; set; }

    public string CreatedByIpHash { get; set; } = string.Empty;

    public string? RevokedByIpHash { get; set; }

    public string UserAgentHash { get; set; } = string.Empty;
}
