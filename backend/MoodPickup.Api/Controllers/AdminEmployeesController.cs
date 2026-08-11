using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanManageEmployees)]
[Route("api/v{version:apiVersion}/admin/employees")]
[Tags("Admin - Employees")]
public sealed class AdminEmployeesController(
    IEmployeeManagementService employeeManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<EmployeeListItemDto>>> Get(
        [FromQuery] EmployeeListQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.GetAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailsDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<CreateEmployeeResponse>> Create(
        CreateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        var response = await employeeManagementService.CreateAsync(
            request,
            cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = response.Employee.Id }, response);
    }

    [HttpGet("{id:guid}/permissions")]
    public async Task<ActionResult<EmployeePermissionsResponse>> GetPermissions(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.GetPermissionsAsync(
            id,
            cancellationToken));
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<ActionResult<EmployeePermissionsResponse>> ReplacePermissions(
        Guid id,
        ReplaceEmployeePermissionsRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.ReplacePermissionOverridesAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<EmployeeDetailsDto>> Update(
        Guid id,
        UpdateEmployeeRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.UpdateAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPost("{id:guid}/disable")]
    public async Task<ActionResult<EmployeeDetailsDto>> Disable(
        Guid id,
        EmployeeVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.DisableAsync(
            id,
            request,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken));
    }

    [HttpPost("{id:guid}/enable")]
    public async Task<ActionResult<EmployeeDetailsDto>> Enable(
        Guid id,
        EmployeeVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.EnableAsync(
            id,
            request,
            cancellationToken));
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ResetEmployeePasswordResponse>> ResetPassword(
        Guid id,
        EmployeeVersionRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.ResetPasswordAsync(
            id,
            request,
            AuthenticationRequestMetadata.FromHttpContext(HttpContext),
            cancellationToken));
    }

    [HttpGet("{id:guid}/actions")]
    public async Task<ActionResult<PagedResponse<EmployeeActionListItemDto>>> GetActions(
        Guid id,
        [FromQuery] EmployeeActionQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.GetActionsAsync(
            id,
            query,
            cancellationToken));
    }
}
