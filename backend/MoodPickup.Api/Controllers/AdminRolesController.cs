using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Employees;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanManageEmployees)]
[Route("api/v{version:apiVersion}/admin/roles")]
[Tags("Admin - Employee Roles")]
public sealed class AdminRolesController(
    IEmployeeManagementService employeeManagementService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<RoleOptionDto>>> Get(
        CancellationToken cancellationToken)
    {
        return Ok(await employeeManagementService.GetRolesAsync(cancellationToken));
    }
}
