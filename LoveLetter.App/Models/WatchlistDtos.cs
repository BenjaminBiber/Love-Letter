using System.ComponentModel.DataAnnotations;
using LoveLetter.App.Data;

namespace LoveLetter.App.Models;

public sealed record WatchlistMovieDto
{
    public required Guid Id { get; init; }
    public required string ImdbId { get; init; }
    public required string Title { get; init; }
    public string? Year { get; init; }
    public string? PosterUrl { get; init; }
    public string? Type { get; init; }
    public string? Plot { get; init; }
    public bool Watched { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime? WatchedAt { get; init; }
}

public sealed record WatchlistSearchResult
{
    public required string ImdbId { get; init; }
    public required string Title { get; init; }
    public string? Year { get; init; }
    public string? PosterUrl { get; init; }
    public string? Type { get; init; }
}

public sealed record AddWatchlistMovieRequest
{
    [Required]
    [MaxLength(24)]
    public string ImdbId { get; init; } = string.Empty;
}

public sealed record SetWatchedRequest
{
    public bool Watched { get; init; }
}

public static class WatchlistMappings
{
    public static WatchlistMovieDto ToDto(this WatchlistMovie movie) => new()
    {
        Id = movie.Id,
        ImdbId = movie.ImdbId,
        Title = movie.Title,
        Year = movie.Year,
        PosterUrl = movie.PosterUrl,
        Type = movie.Type,
        Plot = movie.Plot,
        Watched = movie.Watched,
        CreatedAt = movie.CreatedAt,
        WatchedAt = movie.WatchedAt
    };
}
