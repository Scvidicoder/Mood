namespace MoodPickup.Api.DTOs.Menu;

public sealed record PagedResponse<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

public sealed record MenuIssueDto(
    string Code,
    string Message,
    Guid? ProductOptionGroupId);

public sealed record OrderabilityDto(
    bool IsOrderable,
    IReadOnlyList<MenuIssueDto> Issues);

public sealed record MenuMutationResponse<T>(
    T Resource,
    OrderabilityDto Orderability);

public class PaginationQuery
{
    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
