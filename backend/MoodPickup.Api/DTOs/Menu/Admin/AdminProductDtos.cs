using MoodPickup.Api.DTOs.Menu;

namespace MoodPickup.Api.DTOs.Menu.Admin;

public sealed class AdminProductQuery : PaginationQuery
{
    public Guid? CategoryId { get; init; }

    public string? Search { get; init; }

    public bool? IsAvailable { get; init; }

    public bool? IsVisible { get; init; }

    public bool IncludeDeleted { get; init; }
}

public sealed record AdminProductListItemDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? ImageUrl,
    decimal BasePrice,
    bool IsAvailable,
    bool IsVisible,
    bool IsDeleted,
    bool IsOrderable,
    IReadOnlyList<MenuIssueDto> AvailabilityIssues,
    int DisplayOrder,
    DateTimeOffset UpdatedAt,
    Guid RowVersion);

public sealed record AdminProductDto(
    Guid Id,
    Guid CategoryId,
    string CategoryName,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Ingredients,
    decimal BasePrice,
    int? DefaultWeightGrams,
    int? DefaultVolumeMilliliters,
    int? DefaultCalories,
    Guid? ImageId,
    AdminMediaFileDto? Image,
    bool IsAvailable,
    bool IsVisible,
    bool IsDeleted,
    int DisplayOrder,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    Guid RowVersion,
    OrderabilityDto Orderability,
    IReadOnlyList<AdminProductOptionGroupDto> OptionGroups);

public sealed record AdminMediaFileDto(
    Guid Id,
    string StorageProvider,
    string StorageKey,
    string OriginalFileName,
    string ContentType,
    long FileSizeBytes,
    int? Width,
    int? Height,
    string? Url,
    bool IsDeleted);

public sealed record CreateProductRequest(
    Guid CategoryId,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Ingredients,
    decimal BasePrice,
    int? DefaultWeightGrams,
    int? DefaultVolumeMilliliters,
    int? DefaultCalories,
    Guid? ImageId,
    bool IsAvailable,
    bool IsVisible,
    int DisplayOrder);

public sealed record UpdateProductRequest(
    Guid CategoryId,
    string Name,
    string? ShortDescription,
    string? Description,
    string? Ingredients,
    decimal BasePrice,
    int? DefaultWeightGrams,
    int? DefaultVolumeMilliliters,
    int? DefaultCalories,
    Guid? ImageId,
    bool IsAvailable,
    bool IsVisible,
    int DisplayOrder,
    Guid RowVersion);

public sealed record DuplicateProductRequest(string? Name);

public sealed record AssignProductImageRequest(Guid? ImageId, Guid RowVersion);

public sealed record ReorderProductsRequest(
    Guid CategoryId,
    IReadOnlyList<ReorderItemRequest> Items);
