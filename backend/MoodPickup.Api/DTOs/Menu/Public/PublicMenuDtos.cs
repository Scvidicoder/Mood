using MoodPickup.Api.DTOs.Menu;

namespace MoodPickup.Api.DTOs.Menu.Public;

public sealed record PublicCategoryDto(
    Guid Id,
    string Name,
    string? Description,
    int DisplayOrder);

public sealed class PublicProductQuery : PaginationQuery
{
    public Guid? CategoryId { get; init; }

    public string? Search { get; init; }

    public bool? IncludeUnavailable { get; init; }
}

public sealed record PublicProductListItemDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? ShortDescription,
    string? ImageUrl,
    decimal PriceFrom,
    string Currency,
    int? WeightGrams,
    int? VolumeMilliliters,
    int? Calories,
    bool IsAvailable,
    bool IsOrderable,
    IReadOnlyList<MenuIssueDto> AvailabilityIssues);

public sealed record PublicProductDetailDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    string? Description,
    string? Ingredients,
    string? ImageUrl,
    decimal BasePrice,
    decimal PriceFrom,
    string Currency,
    int? WeightGrams,
    int? VolumeMilliliters,
    int? Calories,
    bool IsAvailable,
    bool IsOrderable,
    IReadOnlyList<MenuIssueDto> AvailabilityIssues,
    IReadOnlyList<PublicProductOptionGroupDto> OptionGroups);

public sealed record PublicProductOptionGroupDto(
    Guid Id,
    string Name,
    string? Description,
    string SelectionType,
    bool IsRequired,
    int MinimumSelections,
    int MaximumSelections,
    int DisplayOrder,
    IReadOnlyList<PublicProductOptionValueDto> Values);

public sealed record PublicProductOptionValueDto(
    Guid Id,
    Guid OptionValueId,
    string Name,
    string? Description,
    decimal PriceModifier,
    bool IsDefault,
    bool IsAvailable,
    int DisplayOrder,
    int? VolumeMilliliters,
    int? Calories);
