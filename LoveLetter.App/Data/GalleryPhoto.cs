using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LoveLetter.App.Data;

public class GalleryPhoto
{
    public Guid Id { get; set; }

    [MaxLength(160)]
    public string? Caption { get; set; }

    [MaxLength(256)]
    public string? OriginalFileName { get; set; }

    [MaxLength(512)]
    public required string FilePath { get; set; }

    [MaxLength(512)]
    public string? ThumbnailPath { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsFavorite { get; set; }

    public DateTime? FavoritedAt { get; set; }

    [NotMapped]
    public string PhotoUrl => FilePath.StartsWith("/", StringComparison.Ordinal)
        ? FilePath
        : $"/{FilePath.Replace("\\", "/")}";

    [NotMapped]
    public string? ThumbnailUrl => string.IsNullOrWhiteSpace(ThumbnailPath)
        ? null
        : ThumbnailPath.StartsWith("/", StringComparison.Ordinal)
            ? ThumbnailPath
            : $"/{ThumbnailPath.Replace("\\", "/")}";
}
