using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.DTOs;
using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Tests;

public sealed class PostgresEmployeeManagementApiTests(
    PostgresMoodPickupApiFactory factory) : IClassFixture<PostgresMoodPickupApiFactory>
{
    private const string NewPassword = "UpdatedEmployee2!";
    private readonly HttpClient _client = factory.CreateSecureClient();

    [PostgresFact]
    public async Task EmployeeManagement_RequiresAdministratorPolicy()
    {
        await factory.ResetAsync(seedMenu: false);

        using var anonymous = await _client.GetAsync("/api/v1/admin/employees");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var customerToken = await factory.CreateCustomerTokenAsync();
        using var customer = await SendAsync(
            HttpMethod.Get,
            "/api/v1/admin/employees",
            customerToken);
        Assert.Equal(HttpStatusCode.Forbidden, customer.StatusCode);

        foreach (var (username, roles) in new[]
                 {
                     ("ordinary", Array.Empty<string>()),
                     ("kitchen-auth", new[] { AuthenticationConstants.Roles.Kitchen }),
                     ("cashier-auth", new[] { AuthenticationConstants.Roles.Cashier }),
                     ("menu-auth", new[] { AuthenticationConstants.Roles.MenuManager }),
                     ("manager-auth", new[] { AuthenticationConstants.Roles.Manager })
                 })
        {
            var token = await factory.CreateEmployeeTokenAsync(username, roles);
            using var forbidden = await SendAsync(
                HttpMethod.Get,
                "/api/v1/admin/employees",
                token);
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        }

        var administratorToken = await factory.GetAdministratorTokenAsync();
        using var allowed = await SendAsync(
            HttpMethod.Get,
            "/api/v1/admin/employees",
            administratorToken);
        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }

    [PostgresFact]
    public async Task CreateEmployee_GeneratesOneTimePasswordAndPreservesFirstLoginFlow()
    {
        await factory.ResetAsync(seedMenu: false);
        var administratorToken = await factory.GetAdministratorTokenAsync();
        var created = await CreateEmployeeAsync(
            administratorToken,
            "Aziz Karimov",
            "aziz.k",
            AuthenticationConstants.Roles.Kitchen,
            AuthenticationConstants.Roles.Pickup);

        Assert.NotEmpty(created.TemporaryPassword);
        Assert.Equal(18, created.TemporaryPassword.Length);
        Assert.True(created.Employee.MustChangePassword);
        Assert.Equal(
            [AuthenticationConstants.Roles.Kitchen, AuthenticationConstants.Roles.Pickup],
            created.Employee.Roles.OrderBy(role => role));

        var stored = await factory.ReadDatabaseAsync(async db =>
            await db.Employees
                .Include(employee => employee.EmployeeRoles)
                .ThenInclude(employeeRole => employeeRole.Role)
                .SingleAsync(employee => employee.Id == created.Employee.Id));
        Assert.NotEqual(created.TemporaryPassword, stored.PasswordHash);
        Assert.DoesNotContain(created.TemporaryPassword, stored.PasswordHash, StringComparison.Ordinal);
        Assert.True(stored.MustChangePassword);
        Assert.False(string.IsNullOrWhiteSpace(stored.PasswordHash));

        using var detailsResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/admin/employees/{created.Employee.Id}",
            administratorToken);
        var detailsJson = await detailsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", detailsJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("temporaryPassword", detailsJson, StringComparison.OrdinalIgnoreCase);

        var temporaryLogin = await LoginAsync("AZIZ.K", created.TemporaryPassword);
        Assert.True(temporaryLogin.MustChangePassword);
        using var blockedKitchen = await SendAsync(
            HttpMethod.Get,
            "/api/v1/staff/kitchen/orders",
            temporaryLogin.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, blockedKitchen.StatusCode);

        using var changed = await SendAsync(
            HttpMethod.Post,
            "/api/v1/staff/auth/change-password",
            temporaryLogin.AccessToken,
            new ChangeEmployeePasswordRequest(created.TemporaryPassword, NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        using var oldPassword = await _client.PostAsJsonAsync(
            "/api/v1/staff/auth/login",
            new EmployeeLoginRequest("aziz.k", created.TemporaryPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        var permanentLogin = await LoginAsync("aziz.k", NewPassword);
        Assert.False(permanentLogin.MustChangePassword);

        using var duplicate = await SendAsync(
            HttpMethod.Post,
            "/api/v1/admin/employees",
            administratorToken,
            new CreateEmployeeRequest(
                "Duplicate Aziz",
                "AZIZ.K",
                [AuthenticationConstants.Roles.Kitchen]));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("EMPLOYEE_USERNAME_CONFLICT", await ProblemCodeAsync(duplicate));

        var auditValues = await factory.ReadDatabaseAsync(async db =>
            await db.EmployeeActionLogs
                .Where(log => log.EntityId == created.Employee.Id)
                .Select(log => new { log.OldValuesJson, log.NewValuesJson })
                .ToListAsync());
        var auditJson = auditValues
            .Select(value => (value.OldValuesJson ?? "") + (value.NewValuesJson ?? ""))
            .ToList();
        Assert.NotEmpty(auditJson);
        Assert.DoesNotContain(
            auditJson,
            value => value.Contains(created.TemporaryPassword, StringComparison.Ordinal));
        Assert.Contains(
            await factory.ReadDatabaseAsync(db => db.EmployeeActionLogs
                .Where(log => log.EntityId == created.Employee.Id)
                .Select(log => log.ActionType)
                .ToListAsync()),
            action => action == "EmployeeCreated");
    }

    [PostgresFact]
    public async Task UpdateEmployee_ValidatesRolesConcurrencyAndLastAdministrator()
    {
        await factory.ResetAsync(seedMenu: false);
        var administratorToken = await factory.GetAdministratorTokenAsync();
        var target = await CreateEmployeeAsync(
            administratorToken,
            "Kitchen Pickup",
            "kitchen.pickup",
            AuthenticationConstants.Roles.Kitchen,
            AuthenticationConstants.Roles.Pickup);

        using var updatedResponse = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{target.Employee.Id}",
            administratorToken,
            new UpdateEmployeeRequest(
                "Kitchen Lead",
                "kitchen.lead",
                [AuthenticationConstants.Roles.Kitchen],
                target.Employee.RowVersion));
        var updated = await ReadAsync<EmployeeDetailsDto>(updatedResponse);
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        Assert.Equal("Kitchen Lead", updated.FullName);
        Assert.Equal("kitchen.lead", updated.Username);
        Assert.Equal([AuthenticationConstants.Roles.Kitchen], updated.Roles);
        Assert.NotEqual(target.Employee.RowVersion, updated.RowVersion);

        using var stale = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{target.Employee.Id}",
            administratorToken,
            new UpdateEmployeeRequest(
                "Stale overwrite",
                "kitchen.stale",
                [AuthenticationConstants.Roles.Pickup],
                target.Employee.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("EMPLOYEE_VERSION_CONFLICT", await ProblemCodeAsync(stale));
        Assert.Equal(
            "Kitchen Lead",
            await factory.ReadDatabaseAsync(async db =>
                (await db.Employees.SingleAsync(employee => employee.Id == target.Employee.Id))
                .FullName));

        using var duplicateRole = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{target.Employee.Id}",
            administratorToken,
            new UpdateEmployeeRequest(
                updated.FullName,
                updated.Username,
                [AuthenticationConstants.Roles.Kitchen, AuthenticationConstants.Roles.Kitchen],
                updated.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, duplicateRole.StatusCode);

        using var invalidRole = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{target.Employee.Id}",
            administratorToken,
            new UpdateEmployeeRequest(
                updated.FullName,
                updated.Username,
                ["InventedRole"],
                updated.RowVersion));
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
        Assert.Equal("INVALID_EMPLOYEE_ROLE", await ProblemCodeAsync(invalidRole));

        var administrator = await GetAdministratorAsync(administratorToken);
        using var lastAdminRemoval = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{administrator.Id}",
            administratorToken,
            new UpdateEmployeeRequest(
                administrator.FullName,
                administrator.Username,
                [AuthenticationConstants.Roles.Manager],
                administrator.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, lastAdminRemoval.StatusCode);
        Assert.Equal("LAST_ADMINISTRATOR_PROTECTION", await ProblemCodeAsync(lastAdminRemoval));

        using var lastAdminDisable = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/employees/{administrator.Id}/disable",
            administratorToken,
            new EmployeeVersionRequest(administrator.RowVersion));
        Assert.Equal(HttpStatusCode.Conflict, lastAdminDisable.StatusCode);
        Assert.Equal("LAST_ADMINISTRATOR_PROTECTION", await ProblemCodeAsync(lastAdminDisable));

        _ = await CreateEmployeeAsync(
            administratorToken,
            "Backup Administrator",
            "backup.admin",
            AuthenticationConstants.Roles.Administrator);
        using var permittedRemoval = await SendAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{administrator.Id}",
            administratorToken,
            new UpdateEmployeeRequest(
                administrator.FullName,
                administrator.Username,
                [AuthenticationConstants.Roles.Manager],
                administrator.RowVersion));
        Assert.Equal(HttpStatusCode.OK, permittedRemoval.StatusCode);
    }

    [PostgresFact]
    public async Task DisableAndEnable_RevokesSessionsAndRejectsPreviouslyIssuedAccessToken()
    {
        await factory.ResetAsync(seedMenu: false);
        var administratorToken = await factory.GetAdministratorTokenAsync();
        var created = await CreateEmployeeAsync(
            administratorToken,
            "Cathy Cashier",
            "cathy.cashier",
            AuthenticationConstants.Roles.Cashier);
        var initial = await LoginAsync(created.Employee.Username, created.TemporaryPassword);
        using var changed = await SendAsync(
            HttpMethod.Post,
            "/api/v1/staff/auth/change-password",
            initial.AccessToken,
            new ChangeEmployeePasswordRequest(created.TemporaryPassword, NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        var activeSession = await LoginAsync(created.Employee.Username, NewPassword);
        using var beforeDisable = await SendAsync(
            HttpMethod.Get,
            "/api/v1/staff/orders",
            activeSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, beforeDisable.StatusCode);

        var current = await GetEmployeeAsync(administratorToken, created.Employee.Id);
        using var disabledResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/employees/{created.Employee.Id}/disable",
            administratorToken,
            new EmployeeVersionRequest(current.RowVersion));
        var disabled = await ReadAsync<EmployeeDetailsDto>(disabledResponse);
        Assert.False(disabled.IsActive);
        Assert.Equal(
            0,
            await factory.ReadDatabaseAsync(db => db.RefreshTokens.CountAsync(token =>
                token.EmployeeId == created.Employee.Id && token.RevokedAt == null)));

        using var staleAccess = await SendAsync(
            HttpMethod.Get,
            "/api/v1/staff/orders",
            activeSession.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, staleAccess.StatusCode);
        using var disabledLogin = await _client.PostAsJsonAsync(
            "/api/v1/staff/auth/login",
            new EmployeeLoginRequest(created.Employee.Username, NewPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, disabledLogin.StatusCode);

        using var enabledResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/employees/{created.Employee.Id}/enable",
            administratorToken,
            new EmployeeVersionRequest(disabled.RowVersion));
        var enabled = await ReadAsync<EmployeeDetailsDto>(enabledResponse);
        Assert.True(enabled.IsActive);

        using var oldSessionStillInvalid = await SendAsync(
            HttpMethod.Get,
            "/api/v1/staff/orders",
            activeSession.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, oldSessionStillInvalid.StatusCode);
        var newSession = await LoginAsync(created.Employee.Username, NewPassword);
        using var afterEnable = await SendAsync(
            HttpMethod.Get,
            "/api/v1/staff/orders",
            newSession.AccessToken);
        Assert.Equal(HttpStatusCode.OK, afterEnable.StatusCode);

        var actions = await factory.ReadDatabaseAsync(db => db.EmployeeActionLogs
            .Where(log => log.EntityId == created.Employee.Id)
            .Select(log => log.ActionType)
            .ToListAsync());
        Assert.Contains("EmployeeDisabled", actions);
        Assert.Contains("EmployeeEnabled", actions);
    }

    [PostgresFact]
    public async Task PasswordReset_ReplacesPasswordRevokesSessionsAndNeverAuditsSecret()
    {
        await factory.ResetAsync(seedMenu: false);
        var administratorToken = await factory.GetAdministratorTokenAsync();
        var created = await CreateEmployeeAsync(
            administratorToken,
            "Manny Manager",
            "manny.manager",
            AuthenticationConstants.Roles.Manager);
        var initial = await LoginAsync(created.Employee.Username, created.TemporaryPassword);
        using var changed = await SendAsync(
            HttpMethod.Post,
            "/api/v1/staff/auth/change-password",
            initial.AccessToken,
            new ChangeEmployeePasswordRequest(created.TemporaryPassword, NewPassword));
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);
        _ = await LoginAsync(created.Employee.Username, NewPassword);
        var current = await GetEmployeeAsync(administratorToken, created.Employee.Id);

        using var resetResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/employees/{created.Employee.Id}/reset-password",
            administratorToken,
            new EmployeeVersionRequest(current.RowVersion));
        var reset = await ReadAsync<ResetEmployeePasswordResponse>(resetResponse);
        Assert.True(reset.MustChangePassword);
        Assert.True(reset.RevokedSessionCount >= 1);
        Assert.NotEmpty(reset.TemporaryPassword);
        Assert.NotEqual(current.RowVersion, reset.RowVersion);

        using var oldPassword = await _client.PostAsJsonAsync(
            "/api/v1/staff/auth/login",
            new EmployeeLoginRequest(created.Employee.Username, NewPassword));
        Assert.Equal(HttpStatusCode.Unauthorized, oldPassword.StatusCode);
        Assert.Equal(
            0,
            await factory.ReadDatabaseAsync(db => db.RefreshTokens.CountAsync(token =>
                token.EmployeeId == created.Employee.Id && token.RevokedAt == null)));
        var temporaryLogin = await LoginAsync(
            created.Employee.Username,
            reset.TemporaryPassword);
        Assert.True(temporaryLogin.MustChangePassword);

        var audit = await factory.ReadDatabaseAsync(async db =>
            await db.EmployeeActionLogs.SingleAsync(log =>
                log.EntityId == created.Employee.Id &&
                log.ActionType == "EmployeePasswordReset"));
        Assert.DoesNotContain(
            reset.TemporaryPassword,
            (audit.OldValuesJson ?? "") + (audit.NewValuesJson ?? ""),
            StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", audit.NewValuesJson ?? "", StringComparison.Ordinal);
    }

    [PostgresFact]
    public async Task EmployeeActions_ArePaginatedFilteredAndContainNoSecrets()
    {
        await factory.ResetAsync(seedMenu: false);
        var administratorToken = await factory.GetAdministratorTokenAsync();
        var created = await CreateEmployeeAsync(
            administratorToken,
            "Audit Employee",
            "audit.employee",
            AuthenticationConstants.Roles.Kitchen);
        var current = await GetEmployeeAsync(administratorToken, created.Employee.Id);
        using var disabledResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/employees/{created.Employee.Id}/disable",
            administratorToken,
            new EmployeeVersionRequest(current.RowVersion));
        var disabled = await ReadAsync<EmployeeDetailsDto>(disabledResponse);
        using var enabledResponse = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/admin/employees/{created.Employee.Id}/enable",
            administratorToken,
            new EmployeeVersionRequest(disabled.RowVersion));
        Assert.Equal(HttpStatusCode.OK, enabledResponse.StatusCode);

        using var pageResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/admin/employees/{created.Employee.Id}/actions?page=1&pageSize=1",
            administratorToken);
        var page = await ReadAsync<PagedResponse<EmployeeActionListItemDto>>(pageResponse);
        Assert.Single(page.Items);
        Assert.True(page.TotalCount >= 3);
        Assert.True(page.TotalPages >= 3);

        using var filteredResponse = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/admin/employees/{created.Employee.Id}/actions?actionType=EmployeeDisabled&entityType=Employee&page=1&pageSize=20",
            administratorToken);
        var filtered = await ReadAsync<PagedResponse<EmployeeActionListItemDto>>(
            filteredResponse);
        var action = Assert.Single(filtered.Items);
        Assert.Equal("EmployeeDisabled", action.ActionType);
        var json = await filteredResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("password", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("token", json, StringComparison.OrdinalIgnoreCase);
    }

    private async Task<CreateEmployeeResponse> CreateEmployeeAsync(
        string administratorToken,
        string fullName,
        string username,
        params string[] roles)
    {
        using var response = await SendAsync(
            HttpMethod.Post,
            "/api/v1/admin/employees",
            administratorToken,
            new CreateEmployeeRequest(fullName, username, roles));
        response.EnsureSuccessStatusCode();
        return await ReadAsync<CreateEmployeeResponse>(response);
    }

    private async Task<EmployeeAuthenticationResponse> LoginAsync(
        string username,
        string password)
    {
        using var response = await _client.PostAsJsonAsync(
            "/api/v1/staff/auth/login",
            new EmployeeLoginRequest(username, password));
        response.EnsureSuccessStatusCode();
        return await ReadAsync<EmployeeAuthenticationResponse>(response);
    }

    private async Task<EmployeeDetailsDto> GetAdministratorAsync(string token)
    {
        using var listResponse = await SendAsync(
            HttpMethod.Get,
            "/api/v1/admin/employees?search=admin&page=1&pageSize=20",
            token);
        var list = await ReadAsync<PagedResponse<EmployeeListItemDto>>(listResponse);
        var id = list.Items.Single(employee => employee.Username == "admin").Id;
        return await GetEmployeeAsync(token, id);
    }

    private async Task<EmployeeDetailsDto> GetEmployeeAsync(string token, Guid id)
    {
        using var response = await SendAsync(
            HttpMethod.Get,
            $"/api/v1/admin/employees/{id}",
            token);
        return await ReadAsync<EmployeeDetailsDto>(response);
    }

    private async Task<HttpResponseMessage> SendAsync(
        HttpMethod method,
        string path,
        string token,
        object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body);
        }

        return await _client.SendAsync(request);
    }

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        return await response.Content.ReadFromJsonAsync<T>()
            ?? throw new InvalidOperationException("The response body was empty.");
    }

    private static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}
