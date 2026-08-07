using Microsoft.Extensions.Options;
using MoodPickup.Api.Extensions;

namespace MoodPickup.Api.Infrastructure;

public sealed class AuthenticationCookieService(
    IOptions<RefreshTokenOptions> options)
{
    private readonly RefreshTokenOptions _options = options.Value;

    public string? GetRefreshToken(HttpRequest request)
    {
        return request.Cookies.TryGetValue(_options.CookieName, out var refreshToken)
            ? refreshToken
            : null;
    }

    public void SetSessionCookies(
        HttpResponse response,
        string refreshToken,
        DateTimeOffset expiresAt)
    {
        var csrfToken = AuthenticationHashing.CreateRandomToken(32);

        response.Cookies.Append(
            _options.CookieName,
            refreshToken,
            CreateCookieOptions(
                expiresAt,
                httpOnly: true,
                path: _options.CookiePath));
        response.Cookies.Append(
            _options.CsrfCookieName,
            csrfToken,
            CreateCookieOptions(
                expiresAt,
                httpOnly: false,
                path: _options.CsrfCookiePath));
    }

    public void ClearSessionCookies(HttpResponse response)
    {
        var expired = DateTimeOffset.UnixEpoch;

        response.Cookies.Delete(
            _options.CookieName,
            CreateCookieOptions(
                expired,
                httpOnly: true,
                path: _options.CookiePath));
        response.Cookies.Delete(
            _options.CsrfCookieName,
            CreateCookieOptions(
                expired,
                httpOnly: false,
                path: _options.CsrfCookiePath));
    }

    private CookieOptions CreateCookieOptions(
        DateTimeOffset expiresAt,
        bool httpOnly,
        string path)
    {
        return new CookieOptions
        {
            HttpOnly = httpOnly,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            Path = path,
            Expires = expiresAt,
            IsEssential = true
        };
    }
}
