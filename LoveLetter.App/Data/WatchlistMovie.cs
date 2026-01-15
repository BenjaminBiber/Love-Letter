using System.ComponentModel.DataAnnotations;

namespace LoveLetter.App.Data;

public class WatchlistMovie
{
    public Guid Id { get; set; }

    [MaxLength(24)]
    public required string ImdbId { get; set; }

    [MaxLength(240)]
    public required string Title { get; set; }

    [MaxLength(12)]
    public string? Year { get; set; }

    [MaxLength(500)]
    public string? PosterUrl { get; set; }

    [MaxLength(40)]
    public string? Type { get; set; }

    [MaxLength(2000)]
    public string? Plot { get; set; }

    public bool Watched { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? WatchedAt { get; set; }
}
