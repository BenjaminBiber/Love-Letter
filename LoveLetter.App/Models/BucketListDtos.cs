using System.ComponentModel.DataAnnotations;
using LoveLetter.App.Data;
using System.Linq;

namespace LoveLetter.App.Models;

public sealed record BucketListEntryDto
{
    public required Guid Id { get; init; }
    public required string Title { get; init; }
    public bool RequiresPhoto { get; init; }
    public bool Completed { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public IReadOnlyList<BucketListMediaDto> Media { get; init; } = Array.Empty<BucketListMediaDto>();
}

public sealed record BucketListMediaDto
{
    public required Guid Id { get; init; }
    public required string Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool IsVideo { get; init; }
    public string? OriginalFileName { get; init; }
    public bool IsInGallery { get; init; }
}

public sealed record CreateBucketListEntryRequest
{
    [Required]
    [StringLength(160)]
    public string Title { get; init; } = string.Empty;
    public bool RequiresPhoto { get; init; }
}

public static class BucketListMappings
{
    public static BucketListEntryDto ToDto(this BucketListEntry entry) => new BucketListEntryDto
    {
        Id = entry.Id,
        Title = entry.Title,
        RequiresPhoto = entry.RequiresPhoto,
        Completed = entry.Completed,
        CreatedAt = entry.CreatedAt,
        CompletedAt = entry.CompletedAt,
                Media = entry.Media
            .OrderBy(m => m.CreatedAt)
            .Select(m => new BucketListMediaDto
            {
                Id = m.Id,
                Url = m.Url,
                ThumbnailUrl = m.ThumbnailUrl,
                IsVideo = m.IsVideo,
                OriginalFileName = m.OriginalFileName,
                IsInGallery = m.IsInGallery
            })
            .ToList()
    };
}
