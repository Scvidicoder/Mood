using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MoodPickup.Api.DTOs.Audit;
using MoodPickup.Api.DTOs.Menu;
using MoodPickup.Api.Infrastructure;
using MoodPickup.Api.Interfaces;

namespace MoodPickup.Api.Controllers;

[ApiController]
[ApiVersion(1.0)]
[Authorize(Policy = AuthenticationConstants.Policies.CanViewAuditLog)]
[Route("api/v{version:apiVersion}/admin/audit-log")]
[Tags("Admin - Audit Log")]
public sealed class AdminAuditLogController(IAuditLogQueryService auditLogService)
    : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResponse<AuditLogListItemDto>>> Get(
        [FromQuery] AuditLogQuery query,
        CancellationToken cancellationToken)
    {
        return Ok(await auditLogService.GetAsync(query, cancellationToken));
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AuditLogDetailDto>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        return Ok(await auditLogService.GetByIdAsync(id, cancellationToken));
    }
}
