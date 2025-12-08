using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoveLetter.App.Data;

public class BucketListMedia
{
    public Guid Id { get; set; }

    public Guid EntryId { get; set; }

    public BucketListEntry? Entry { get; set; }

    [MaxLength(512)]
    public required string FilePath { get; set; }

    [MaxLength(512)]
    public string? ThumbnailPath { get; set; }

    [MaxLength(256)]
    public string? OriginalFileName { get; set; }

    [MaxLength(128)]
    public string? ContentType { get; set; }

    public bool IsVideo { get; set; }

    public bool IsInGallery { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public string Url => FilePath.StartsWith("/", StringComparison.Ordinal)
        ? FilePath
        : $"/{FilePath.Replace("\\", "/")}";

    [NotMapped]
    public string? ThumbnailUrl => string.IsNullOrWhiteSpace(ThumbnailPath)
        ? null
        : ThumbnailPath.StartsWith("/", StringComparison.Ordinal)
            ? ThumbnailPath
            : $"/{ThumbnailPath.Replace("\\", "/")}";
}
