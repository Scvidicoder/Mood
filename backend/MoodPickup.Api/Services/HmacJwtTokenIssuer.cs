using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MoodPickup.Api.Entities;
using MoodPickup.Api.Extensions;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Services;

public sealed class HmacJwtTokenIssuer(
    IOptions<JwtOptions> options,
    TimeProvider timeProvider) : ITokenIssuer
{
    private readonly JwtOptions _options = options.Value;

    public IssuedAccessToken IssueCustomerAccessToken(Customer customer)
    {
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, customer.Id.ToString()),
            new Claim(AuthenticationConstants.AccountTypeClaim, AuthenticationConstants.AccountTypes.Customer),
            new Claim(JwtRegisteredClaimNames.PhoneNumber, customer.PhoneNumber)
        };

        return IssueAccessToken(claims);
    }

    public IssuedAccessToken IssueEmployeeAccessToken(
        Employee employee,
        IReadOnlyCollection<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, employee.Id.ToString()),
            new(AuthenticationConstants.AccountTypeClaim, AuthenticationConstants.AccountTypes.Employee),
            new(JwtRegisteredClaimNames.UniqueName, employee.Username),
            new(JwtRegisteredClaimNames.Name, employee.FullName),
            new(
                AuthenticationConstants.EmployeeSessionVersionClaim,
                employee.SessionVersion.ToString()),
            new(
                AuthenticationConstants.MustChangePasswordClaim,
                employee.MustChangePassword.ToString().ToLowerInvariant())
        };

        claims.AddRange(roles.Select(role => new Claim("roles", role)));
        return IssueAccessToken(claims);
    }

    public string IssueRegistrationToken(string phoneNumber, Guid challengeId)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.RegistrationTokenLifetimeMinutes);
        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, phoneNumber),
            new Claim(AuthenticationConstants.TokenUseClaim, AuthenticationConstants.RegistrationTokenUse),
            new Claim(AuthenticationConstants.ChallengeIdClaim, challengeId.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now.UtcDateTime).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        };

        return WriteToken(claims, now, expiresAt);
    }

    public RegistrationTokenClaims ValidateRegistrationToken(string token)
    {
        try
        {
            var handler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            var principal = handler.ValidateToken(
                token,
                CreateTokenValidationParameters(),
                out var validatedToken);

            if (validatedToken is not JwtSecurityToken jwt ||
                !string.Equals(
                    jwt.Header.Alg,
                    SecurityAlgorithms.HmacSha256,
                    StringComparison.Ordinal))
            {
                throw InvalidRegistrationToken();
            }

            var tokenUse = principal.FindFirstValue(AuthenticationConstants.TokenUseClaim);
            var phoneNumber = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var challengeValue = principal.FindFirstValue(AuthenticationConstants.ChallengeIdClaim);

            if (tokenUse != AuthenticationConstants.RegistrationTokenUse ||
                string.IsNullOrWhiteSpace(phoneNumber) ||
                !Guid.TryParse(challengeValue, out var challengeId))
            {
                throw InvalidRegistrationToken();
            }

            return new RegistrationTokenClaims(phoneNumber, challengeId);
        }
        catch (SecurityTokenException)
        {
            throw InvalidRegistrationToken();
        }
        catch (ArgumentException)
        {
            throw InvalidRegistrationToken();
        }
    }

    public TokenValidationParameters CreateTokenValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = CreateSecurityKey(),
            ValidateLifetime = true,
            RequireExpirationTime = true,
            RequireSignedTokens = true,
            ClockSkew = TimeSpan.FromSeconds(_options.ClockSkewSeconds),
            NameClaimType = JwtRegisteredClaimNames.UniqueName,
            RoleClaimType = "roles"
        };
    }

    private IssuedAccessToken IssueAccessToken(IEnumerable<Claim> accountClaims)
    {
        var now = timeProvider.GetUtcNow();
        var expiresAt = now.AddMinutes(_options.AccessTokenLifetimeMinutes);
        var claims = accountClaims.Concat(
        [
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim(
                JwtRegisteredClaimNames.Iat,
                EpochTime.GetIntDate(now.UtcDateTime).ToString(CultureInfo.InvariantCulture),
                ClaimValueTypes.Integer64)
        ]);

        return new IssuedAccessToken(
            WriteToken(claims, now, expiresAt),
            checked((int)(expiresAt - now).TotalSeconds));
    }

    private string WriteToken(
        IEnumerable<Claim> claims,
        DateTimeOffset issuedAt,
        DateTimeOffset expiresAt)
    {
        var credentials = new SigningCredentials(
            CreateSecurityKey(),
            SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: issuedAt.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private SymmetricSecurityKey CreateSecurityKey()
    {
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
    }

    private static ApiProblemException InvalidRegistrationToken()
    {
        return new ApiProblemException(
            StatusCodes.Status401Unauthorized,
            "invalid_registration_token",
            "Registration session is invalid or expired",
            "INVALID_REGISTRATION_TOKEN");
    }
}
