using LoveLetter.App.Data;

namespace LoveLetter.App.Models;

public sealed record GalleryAlbumDto
{
    public Guid? Id { get; init; }
    public required string Name { get; init; }
    public int PhotoCount { get; init; }
    public string? CoverUrl { get; init; }
    public string? CoverThumbnailUrl { get; init; }
    public bool IsFavorite { get; init; }
    public bool IsUnassigned { get; init; }

    public static GalleryAlbumDto FromFavorite(string name, int count, GalleryPhoto? cover) =>
        new()
        {
            Id = null,
            Name = name,
            PhotoCount = count,
            CoverUrl = cover?.PhotoUrl,
            CoverThumbnailUrl = cover?.ThumbnailUrl,
            IsFavorite = true,
            IsUnassigned = false
        };

    public static GalleryAlbumDto FromUnassigned(int count, GalleryPhoto? cover) =>
        new()
        {
            Id = null,
            Name = GalleryPhoto.UnassignedAlbumName,
            PhotoCount = count,
            CoverUrl = cover?.PhotoUrl,
            CoverThumbnailUrl = cover?.ThumbnailUrl,
            IsFavorite = false,
            IsUnassigned = true
        };
}
