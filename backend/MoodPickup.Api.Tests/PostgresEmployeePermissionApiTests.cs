using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.Infrastructure;

namespace MoodPickup.Api.Tests;

public sealed class PostgresEmployeePermissionApiTests(
    PostgresMoodPickupApiFactory factory) : IClassFixture<PostgresMoodPickupApiFactory>
{
    private readonly HttpClient _client = factory.CreateSecureClient();

    [PostgresFact]
    public async Task GetPermissions_ReturnsRoleDefaults()
    {
        await factory.ResetAsync(seedMenu: false);
        _ = await factory.CreateEmployeeTokenAsync(
            "permission-cashier",
            AuthenticationConstants.Roles.Cashier);
        var employeeId = await GetEmployeeIdAsync("permission-cashier");

        using var response = await SendAsAdministratorAsync(
            HttpMethod.Get,
            $"/api/v1/admin/employees/{employeeId}/permissions");
        var result = await ReadAsync<EmployeePermissionsResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(employeeId, result.EmployeeId);
        var reject = Assert.Single(
            result.Permissions,
            permission =>
                permission.Permission == EmployeePermissionCatalog.RejectOrders);
        Assert.True(reject.RoleAllowed);
        Assert.Null(reject.Override);
        Assert.True(reject.IsAllowed);
    }

    [PostgresFact]
    public async Task UpdatePermissions_ReplacesOverridesAndWritesAudit()
    {
        await factory.ResetAsync(seedMenu: false);
        _ = await factory.CreateEmployeeTokenAsync(
            "permission-update",
            AuthenticationConstants.Roles.Cashier);
        var employeeId = await GetEmployeeIdAsync("permission-update");
        var request = new ReplaceEmployeePermissionsRequest(
        [
            new EmployeePermissionOverrideRequest(
                EmployeePermissionCatalog.RejectOrders,
                false),
            new EmployeePermissionOverrideRequest(
                EmployeePermissionCatalog.ViewReports,
                true)
        ]);

        using var response = await SendAsAdministratorAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{employeeId}/permissions",
            request);
        var result = await ReadAsync<EmployeePermissionsResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(result.Permissions.Single(permission =>
            permission.Permission == EmployeePermissionCatalog.RejectOrders).IsAllowed);
        Assert.True(result.Permissions.Single(permission =>
            permission.Permission == EmployeePermissionCatalog.ViewReports).IsAllowed);
        Assert.Equal(2, await factory.ReadDatabaseAsync(db =>
            db.EmployeePermissions.CountAsync(permission =>
                permission.EmployeeId == employeeId)));
        Assert.True(await factory.ReadDatabaseAsync(db =>
            db.EmployeeActionLogs.AnyAsync(log =>
                log.EntityId == employeeId &&
                log.ActionType == "EmployeePermissionsUpdated")));
    }

    [PostgresFact]
    public async Task ResetPermissions_EmptyReplacementRestoresRoleDefaults()
    {
        await factory.ResetAsync(seedMenu: false);
        _ = await factory.CreateEmployeeTokenAsync(
            "permission-reset",
            AuthenticationConstants.Roles.Cashier);
        var employeeId = await GetEmployeeIdAsync("permission-reset");
        _ = await ReplaceAsync(
            employeeId,
            new EmployeePermissionOverrideRequest(
                EmployeePermissionCatalog.RejectOrders,
                false));

        var reset = await ReplaceAsync(employeeId);
        var reject = reset.Permissions.Single(permission =>
            permission.Permission == EmployeePermissionCatalog.RejectOrders);

        Assert.True(reject.RoleAllowed);
        Assert.Null(reject.Override);
        Assert.True(reject.IsAllowed);
        Assert.Equal(0, await factory.ReadDatabaseAsync(db =>
            db.EmployeePermissions.CountAsync(permission =>
                permission.EmployeeId == employeeId)));
    }

    [PostgresFact]
    public async Task PermissionOverride_BlocksRoleGrantedEndpoint()
    {
        await factory.ResetAsync(seedMenu: false);
        var employeeToken = await factory.CreateEmployeeTokenAsync(
            "permission-block",
            AuthenticationConstants.Roles.Cashier);
        var employeeId = await GetEmployeeIdAsync("permission-block");
        _ = await ReplaceAsync(
            employeeId,
            new EmployeePermissionOverrideRequest(
                EmployeePermissionCatalog.RejectOrders,
                false));

        using var response = await SendAsync(
            HttpMethod.Post,
            $"/api/v1/staff/orders/{Guid.NewGuid()}/reject",
            employeeToken,
            new { reason = "Permission test" });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [PostgresFact]
    public async Task PermissionOverride_GrantsEndpointWithoutRoleDefault()
    {
        await factory.ResetAsync(seedMenu: false);
        var employeeToken = await factory.CreateEmployeeTokenAsync(
            "permission-grant",
            AuthenticationConstants.Roles.Kitchen);
        var employeeId = await GetEmployeeIdAsync("permission-grant");
        _ = await ReplaceAsync(
            employeeId,
            new EmployeePermissionOverrideRequest(
                EmployeePermissionCatalog.ViewOrders,
                true));

        using var response = await SendAsync(
            HttpMethod.Get,
            "/api/v1/staff/orders?page=1&pageSize=20",
            employeeToken);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<EmployeePermissionsResponse> ReplaceAsync(
        Guid employeeId,
        params EmployeePermissionOverrideRequest[] overrides)
    {
        using var response = await SendAsAdministratorAsync(
            HttpMethod.Put,
            $"/api/v1/admin/employees/{employeeId}/permissions",
            new ReplaceEmployeePermissionsRequest(overrides));
        response.EnsureSuccessStatusCode();
        return await ReadAsync<EmployeePermissionsResponse>(response);
    }

    private async Task<Guid> GetEmployeeIdAsync(string username)
    {
        return await factory.ReadDatabaseAsync(async db =>
            await db.Employees
                .Where(employee => employee.Username == username)
                .Select(employee => employee.Id)
                .SingleAsync());
    }

    private async Task<HttpResponseMessage> SendAsAdministratorAsync(
        HttpMethod method,
        string path,
        object? body = null)
    {
        return await SendAsync(
            method,
            path,
            await factory.GetAdministratorTokenAsync(),
            body);
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
}
