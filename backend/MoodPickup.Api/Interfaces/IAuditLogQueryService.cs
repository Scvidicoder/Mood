using MoodPickup.Api.DTOs.Audit;
using MoodPickup.Api.DTOs.Menu;

namespace MoodPickup.Api.Interfaces;

public interface IAuditLogQueryService
{
    Task<PagedResponse<AuditLogListItemDto>> GetAsync(
        AuditLogQuery query,
        CancellationToken cancellationToken);

    Task<AuditLogDetailDto> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken);
}
