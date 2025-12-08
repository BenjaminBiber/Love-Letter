using System.ComponentModel.DataAnnotations;
using LoveLetter.App.Data;

namespace LoveLetter.App.Models;

public sealed record GalleryPhotoDto
{
    public required Guid Id { get; init; }
    public string? Caption { get; init; }
    public required string Url { get; init; }
    public string? ThumbnailUrl { get; init; }
    public bool IsFavorite { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? FavoritedAt { get; init; }
}

public sealed record SetGalleryFavoriteRequest
{
    public bool IsFavorite { get; init; }
}

public static class GalleryPhotoMappings
{
    public static GalleryPhotoDto ToDto(this GalleryPhoto photo) => new GalleryPhotoDto
    {
        Id = photo.Id,
        Caption = photo.Caption,
        Url = photo.PhotoUrl,
        ThumbnailUrl = photo.ThumbnailUrl,
        IsFavorite = photo.IsFavorite,
        CreatedAt = photo.CreatedAt,
        FavoritedAt = photo.FavoritedAt
    };
}
