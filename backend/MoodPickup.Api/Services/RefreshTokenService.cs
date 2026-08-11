using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MoodPickup.Api.Data;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Services;

public sealed class RefreshTokenService(
    MoodPickupDbContext dbContext,
    AuthenticationHashing hashing,
    IOptions<RefreshTokenOptions> options,
    TimeProvider timeProvider,
    ILogger<RefreshTokenService> logger)
{
    private readonly RefreshTokenOptions _options = options.Value;

    public async Task<IssuedRefreshToken> IssueAsync(
        AccountType accountType,
        Guid accountId,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        return await IssueInternalAsync(
            accountType,
            accountId,
            Guid.NewGuid(),
            metadata,
            cancellationToken);
    }

    public async Task<RotatedRefreshToken> RotateAsync(
        string rawToken,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var tokenHash = AuthenticationHashing.HashRefreshToken(rawToken);
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == tokenHash,
                cancellationToken);

        if (token is null)
        {
            throw InvalidRefreshToken();
        }

        if (token.RevokedAt is not null)
        {
            await RevokeFamilyAsync(
                token.FamilyId,
                now,
                hashing.HashMetadata(metadata.IpAddress),
                cancellationToken);
            logger.LogWarning(
                "Refresh-token reuse detected for family {RefreshTokenFamilyId}.",
                token.FamilyId);

            throw new ApiProblemException(
                StatusCodes.Status401Unauthorized,
                "refresh_token_reuse",
                "The session has been revoked",
                "REFRESH_TOKEN_REUSE");
        }

        if (token.ExpiresAt <= now)
        {
            token.RevokedAt = now;
            token.RevokedByIpHash = hashing.HashMetadata(metadata.IpAddress);
            await dbContext.SaveChangesAsync(cancellationToken);
            throw InvalidRefreshToken();
        }

        var replacement = CreateToken(
            token.AccountType,
            token.CustomerId ?? token.EmployeeId ??
                throw new InvalidOperationException("Refresh token has no account owner."),
            token.FamilyId,
            metadata,
            now);

        token.RevokedAt = now;
        token.RevokedByIpHash = hashing.HashMetadata(metadata.IpAddress);
        token.ReplacedByTokenId = replacement.Entity.Id;
        dbContext.RefreshTokens.Add(replacement.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new RotatedRefreshToken(
            replacement.RawToken,
            replacement.Entity.ExpiresAt,
            token.AccountType,
            token.CustomerId ?? token.EmployeeId!.Value);
    }

    public async Task RevokeAsync(
        string? rawToken,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken))
        {
            return;
        }

        var tokenHash = AuthenticationHashing.HashRefreshToken(rawToken);
        var token = await dbContext.RefreshTokens
            .SingleOrDefaultAsync(
                refreshToken => refreshToken.TokenHash == tokenHash,
                cancellationToken);

        if (token is null || token.RevokedAt is not null)
        {
            return;
        }

        token.RevokedAt = timeProvider.GetUtcNow();
        token.RevokedByIpHash = hashing.HashMetadata(metadata.IpAddress);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task RevokeEmployeeSessionsAsync(
        Guid employeeId,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        await MarkEmployeeSessionsRevokedAsync(
            employeeId,
            metadata,
            cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task<int> MarkEmployeeSessionsRevokedAsync(
        Guid employeeId,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        var ipHash = hashing.HashMetadata(metadata.IpAddress);
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.EmployeeId == employeeId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var token in activeTokens)
        {
            token.RevokedAt = now;
            token.RevokedByIpHash = ipHash;
        }

        return activeTokens.Count;
    }

    private async Task<IssuedRefreshToken> IssueInternalAsync(
        AccountType accountType,
        Guid accountId,
        Guid familyId,
        AuthenticationRequestMetadata metadata,
        CancellationToken cancellationToken)
    {
        var token = CreateToken(
            accountType,
            accountId,
            familyId,
            metadata,
            timeProvider.GetUtcNow());
        dbContext.RefreshTokens.Add(token.Entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new IssuedRefreshToken(
            token.RawToken,
            token.Entity.ExpiresAt);
    }

    private PendingRefreshToken CreateToken(
        AccountType accountType,
        Guid accountId,
        Guid familyId,
        AuthenticationRequestMetadata metadata,
        DateTimeOffset now)
    {
        var rawToken = AuthenticationHashing.CreateRandomToken();
        var lifetimeDays = accountType == AccountType.Customer
            ? _options.CustomerLifetimeDays
            : _options.EmployeeLifetimeDays;
        var entity = new RefreshToken
        {
            Id = Guid.NewGuid(),
            FamilyId = familyId,
            AccountType = accountType,
            CustomerId = accountType == AccountType.Customer ? accountId : null,
            EmployeeId = accountType == AccountType.Employee ? accountId : null,
            TokenHash = AuthenticationHashing.HashRefreshToken(rawToken),
            CreatedAt = now,
            ExpiresAt = now.AddDays(lifetimeDays),
            CreatedByIpHash = hashing.HashMetadata(metadata.IpAddress),
            UserAgentHash = hashing.HashMetadata(metadata.UserAgent)
        };

        return new PendingRefreshToken(rawToken, entity);
    }

    private async Task RevokeFamilyAsync(
        Guid familyId,
        DateTimeOffset revokedAt,
        string revokedByIpHash,
        CancellationToken cancellationToken)
    {
        var activeTokens = await dbContext.RefreshTokens
            .Where(token => token.FamilyId == familyId && token.RevokedAt == null)
            .ToListAsync(cancellationToken);

        foreach (var activeToken in activeTokens)
        {
            activeToken.RevokedAt = revokedAt;
            activeToken.RevokedByIpHash = revokedByIpHash;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static ApiProblemException InvalidRefreshToken()
    {
        return new ApiProblemException(
            StatusCodes.Status401Unauthorized,
            "invalid_refresh_token",
            "The session is invalid or expired",
            "INVALID_REFRESH_TOKEN");
    }

    private sealed record PendingRefreshToken(
        string RawToken,
        RefreshToken Entity);
}

public sealed record IssuedRefreshToken(
    string RawToken,
    DateTimeOffset ExpiresAt);

public sealed record RotatedRefreshToken(
    string RawToken,
    DateTimeOffset ExpiresAt,
    AccountType AccountType,
    Guid AccountId);
