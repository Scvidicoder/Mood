using MoodPickup.Api.DTOs.Menu;

namespace MoodPickup.Api.DTOs.Menu.Admin;

public sealed class AdminCategoryQuery : PaginationQuery
{
    public bool IncludeDeleted { get; init; }

    public string? Search { get; init; }
}

public sealed record AdminCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsVisible,
    bool IsDeleted,
    int ProductCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid RowVersion);

public sealed record CreateCategoryRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsVisible);

public sealed record UpdateCategoryRequest(
    string Name,
    string? Description,
    int DisplayOrder,
    bool IsVisible,
    Guid RowVersion);

public sealed record SetVisibilityRequest(bool IsVisible, Guid RowVersion);

public sealed record SetAvailabilityRequest(bool IsAvailable, Guid RowVersion);

public sealed record RowVersionRequest(Guid RowVersion);

public sealed record ReorderItemRequest(
    Guid Id,
    int DisplayOrder,
    Guid RowVersion);

public sealed record ReorderCategoriesRequest(
    IReadOnlyList<ReorderItemRequest> Items);
