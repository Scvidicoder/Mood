namespace MoodPickup.Api.Entities;

public sealed class MediaFile : IHasCreatedAt
{
    public Guid Id { get; set; }

    public string StorageProvider { get; set; } = string.Empty;

    public string StorageKey { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public int? Width { get; set; }

    public int? Height { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public Guid? CreatedByEmployeeId { get; set; }

    public Employee? CreatedByEmployee { get; set; }

    public bool IsDeleted { get; set; }

    public ICollection<Product> Products { get; set; } = [];
}
