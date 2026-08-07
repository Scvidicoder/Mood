using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Tests;

public sealed class AuthenticationEndpointsTests(MoodPickupApiFactory factory)
    : IClassFixture<MoodPickupApiFactory>
{
    private const string RefreshCookieName = "__Secure-MoodPickup.Refresh";
    private const string CsrfCookieName = "__Secure-MoodPickup.Csrf";
    private const string CsrfHeaderName = "X-CSRF-TOKEN";
    private readonly HttpClient _client = factory.CreateSecureClient();

    [Fact]
    public async Task Registration_CreatesCustomerAndUsesCookieOnlyRefreshToken()
    {
        await factory.ResetAuthenticationStateAsync();

        var session = await RegisterCustomerAsync("+992900000001", "Amina");

        Assert.False(string.IsNullOrWhiteSpace(session.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(session.RefreshToken));
        Assert.False(string.IsNullOrWhiteSpace(session.CsrfToken));
        Assert.DoesNotContain("refreshToken", session.ResponseJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("\"name\":\"Amina\"", session.ResponseJson, StringComparison.Ordinal);
        Assert.Contains(
            "path=/api/v1/auth",
            session.RefreshCookieHeader,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "httponly",
            session.RefreshCookieHeader,
            StringComparison.OrdinalIgnoreCase);
        Assert.Contains(
            "path=/",
            session.CsrfCookieHeader,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExistingCustomer_CanRequestAndVerifyANewLoginCode()
    {
        await factory.ResetAuthenticationStateAsync();
        await RegisterCustomerAsync("+992900000002", "Farid");
        factory.TimeProvider.Advance(TimeSpan.FromSeconds(61));

        var challenge = await RequestCodeAsync("+992900000002");
        using var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/verify-code",
            new VerifyCustomerCodeRequest(
                challenge.ChallengeId,
                factory.OtpSender.GetCode(challenge.ChallengeId)));
        var payload = await verifyResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, verifyResponse.StatusCode);
        Assert.Contains("\"isNewCustomer\":false", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", payload, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(
            GetSetCookieValue(verifyResponse, RefreshCookieName)));
    }

    [Fact]
    public async Task ExpiredOtp_IsRejected()
    {
        await factory.ResetAuthenticationStateAsync();
        var challenge = await RequestCodeAsync("+992900000003");
        factory.TimeProvider.Advance(TimeSpan.FromMinutes(6));

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/verify-code",
            new VerifyCustomerCodeRequest(
                challenge.ChallengeId,
                factory.OtpSender.GetCode(challenge.ChallengeId)));

        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        Assert.Equal("CODE_EXPIRED", await GetProblemCodeAsync(response));
    }

    [Fact]
    public async Task OtpRetryLimit_BlocksTheFifthFailedAttempt()
    {
        await factory.ResetAuthenticationStateAsync();
        var challenge = await RequestCodeAsync("+992900000004");
        var actualCode = factory.OtpSender.GetCode(challenge.ChallengeId);
        var invalidCode = actualCode == "000000" ? "000001" : "000000";

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            using var response = await _client.PostAsJsonAsync(
                "/api/v1/auth/customer/verify-code",
                new VerifyCustomerCodeRequest(challenge.ChallengeId, invalidCode));

            Assert.Equal(
                attempt == 5
                    ? HttpStatusCode.TooManyRequests
                    : HttpStatusCode.BadRequest,
                response.StatusCode);
        }
    }

    [Fact]
    public async Task Refresh_RotatesTokenAndReuseRevokesTheFamily()
    {
        await factory.ResetAuthenticationStateAsync();
        var initial = await RegisterCustomerAsync("+992900000005", "Nargis");

        using var refreshResponse = await SendSessionRequestAsync(
            "/api/v1/auth/refresh",
            initial.RefreshToken,
            initial.CsrfToken);
        var rotatedRefresh = GetSetCookieValue(refreshResponse, RefreshCookieName);
        var rotatedCsrf = GetSetCookieValue(refreshResponse, CsrfCookieName);

        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        Assert.NotEqual(initial.RefreshToken, rotatedRefresh);

        using var reuseResponse = await SendSessionRequestAsync(
            "/api/v1/auth/refresh",
            initial.RefreshToken,
            initial.CsrfToken);
        Assert.Equal(HttpStatusCode.Unauthorized, reuseResponse.StatusCode);
        Assert.Equal("REFRESH_TOKEN_REUSE", await GetProblemCodeAsync(reuseResponse));

        using var familyResponse = await SendSessionRequestAsync(
            "/api/v1/auth/refresh",
            rotatedRefresh,
            rotatedCsrf);
        Assert.Equal(HttpStatusCode.Unauthorized, familyResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_RequiresMatchingDoubleSubmitCsrfToken()
    {
        await factory.ResetAuthenticationStateAsync();
        var session = await RegisterCustomerAsync("+992900000007", "Zebo");
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/auth/refresh");
        request.Headers.Add(
            "Cookie",
            $"{RefreshCookieName}={session.RefreshToken}; {CsrfCookieName}={session.CsrfToken}");

        using var response = await _client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("CSRF_VALIDATION_FAILED", await GetProblemCodeAsync(response));
    }

    [Fact]
    public async Task Logout_RevokesTheCurrentRefreshToken()
    {
        await factory.ResetAuthenticationStateAsync();
        var session = await RegisterCustomerAsync("+992900000006", "Rustam");

        using var logoutResponse = await SendSessionRequestAsync(
            "/api/v1/auth/logout",
            session.RefreshToken,
            session.CsrfToken);
        using var refreshResponse = await SendSessionRequestAsync(
            "/api/v1/auth/refresh",
            session.RefreshToken,
            session.CsrfToken);

        Assert.Equal(HttpStatusCode.NoContent, logoutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task EmployeeLogin_ReturnsAccessTokenAndCookieOnlyRefreshToken()
    {
        await factory.ResetAuthenticationStateAsync();

        using var response = await _client.PostAsJsonAsync(
            "/api/v1/staff/auth/login",
            new EmployeeLoginRequest("ADMIN", "TestingAdmin1!"));
        var payload = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("\"accessToken\":", payload, StringComparison.Ordinal);
        Assert.Contains("\"Administrator\"", payload, StringComparison.Ordinal);
        Assert.DoesNotContain("refreshToken", payload, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(
            GetSetCookieValue(response, RefreshCookieName)));
    }

    [Fact]
    public async Task AuthorizationPolicies_RespectRolesAndAdministratorOverride()
    {
        await factory.ResetAuthenticationStateAsync();
        var authorization = factory.Services.GetRequiredService<IAuthorizationService>();
        var administrator = CreateEmployeePrincipal(
            AuthenticationConstants.Roles.Administrator);
        var kitchen = CreateEmployeePrincipal(AuthenticationConstants.Roles.Kitchen);

        foreach (var policy in new[]
                 {
                     AuthenticationConstants.Policies.CanReceiveOrders,
                     AuthenticationConstants.Policies.CanWorkKitchen,
                     AuthenticationConstants.Policies.CanIssueOrders,
                     AuthenticationConstants.Policies.CanManageMenu,
                     AuthenticationConstants.Policies.CanManageEmployees,
                     AuthenticationConstants.Policies.CanManageCafeSettings,
                     AuthenticationConstants.Policies.CanViewAuditLog
                 })
        {
            Assert.True((await authorization.AuthorizeAsync(
                administrator,
                resource: null,
                policy)).Succeeded);
        }

        Assert.True((await authorization.AuthorizeAsync(
            kitchen,
            resource: null,
            AuthenticationConstants.Policies.CanWorkKitchen)).Succeeded);
        Assert.False((await authorization.AuthorizeAsync(
            kitchen,
            resource: null,
            AuthenticationConstants.Policies.CanManageMenu)).Succeeded);
    }

    private async Task<RequestCustomerCodeResponse> RequestCodeAsync(string phoneNumber)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/request-code",
            new RequestCustomerCodeRequest(phoneNumber));
        response.EnsureSuccessStatusCode();

        return await response.Content.ReadFromJsonAsync<RequestCustomerCodeResponse>()
            ?? throw new InvalidOperationException("Request-code response was empty.");
    }

    private async Task<TestSession> RegisterCustomerAsync(
        string phoneNumber,
        string name)
    {
        var challenge = await RequestCodeAsync(phoneNumber);
        using var verifyResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/verify-code",
            new VerifyCustomerCodeRequest(
                challenge.ChallengeId,
                factory.OtpSender.GetCode(challenge.ChallengeId)));
        if (!verifyResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Verify code failed with {(int)verifyResponse.StatusCode}: " +
                await verifyResponse.Content.ReadAsStringAsync());
        }
        var verification = await verifyResponse.Content
            .ReadFromJsonAsync<CustomerVerificationResponse>()
            ?? throw new InvalidOperationException("Verify-code response was empty.");

        Assert.True(verification.IsNewCustomer);
        Assert.False(string.IsNullOrWhiteSpace(verification.RegistrationToken));

        using var registrationResponse = await _client.PostAsJsonAsync(
            "/api/v1/auth/customer/complete-registration",
            new CompleteCustomerRegistrationRequest(
                verification.RegistrationToken!,
                name));
        registrationResponse.EnsureSuccessStatusCode();
        var responseJson = await registrationResponse.Content.ReadAsStringAsync();
        var authentication = JsonSerializer.Deserialize<CustomerAuthenticationResponse>(
            responseJson,
            new JsonSerializerOptions(JsonSerializerDefaults.Web))
            ?? throw new InvalidOperationException("Registration response was empty.");

        return new TestSession(
            authentication.AccessToken,
            GetSetCookieValue(registrationResponse, RefreshCookieName),
            GetSetCookieValue(registrationResponse, CsrfCookieName),
            responseJson,
            GetSetCookieHeader(registrationResponse, RefreshCookieName),
            GetSetCookieHeader(registrationResponse, CsrfCookieName));
    }

    private async Task<HttpResponseMessage> SendSessionRequestAsync(
        string path,
        string refreshToken,
        string csrfToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path);
        request.Headers.Add(
            "Cookie",
            $"{RefreshCookieName}={refreshToken}; {CsrfCookieName}={csrfToken}");
        request.Headers.Add(CsrfHeaderName, csrfToken);
        return await _client.SendAsync(request);
    }

    private static string GetSetCookieValue(
        HttpResponseMessage response,
        string cookieName)
    {
        var cookie = GetSetCookieHeader(response, cookieName);
        var valueStart = cookieName.Length + 1;
        var valueEnd = cookie.IndexOf(';', valueStart);

        return cookie[valueStart..valueEnd];
    }

    private static string GetSetCookieHeader(
        HttpResponseMessage response,
        string cookieName)
    {
        return response.Headers
            .GetValues("Set-Cookie")
            .Single(value => value.StartsWith($"{cookieName}=", StringComparison.Ordinal));
    }

    private static async Task<string?> GetProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(
            await response.Content.ReadAsStringAsync());

        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }

    private static ClaimsPrincipal CreateEmployeePrincipal(params string[] roles)
    {
        var claims = new List<Claim>
        {
            new(AuthenticationConstants.AccountTypeClaim, AuthenticationConstants.AccountTypes.Employee),
            new(AuthenticationConstants.MustChangePasswordClaim, "false")
        };
        claims.AddRange(roles.Select(role => new Claim("roles", role)));

        return new ClaimsPrincipal(
            new ClaimsIdentity(
                claims,
                authenticationType: "Test",
                nameType: ClaimTypes.Name,
                roleType: "roles"));
    }

    private sealed record TestSession(
        string AccessToken,
        string RefreshToken,
        string CsrfToken,
        string ResponseJson,
        string RefreshCookieHeader,
        string CsrfCookieHeader);
}
