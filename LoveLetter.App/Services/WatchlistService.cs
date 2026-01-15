using System.Net.Http.Json;
using LoveLetter.App.Configuration;
using LoveLetter.App.Data;
using LoveLetter.App.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace LoveLetter.App.Services;

public interface IWatchlistService
{
    Task<ServiceResult<IReadOnlyList<WatchlistMovieDto>>> GetAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<IReadOnlyList<WatchlistSearchResult>>> SearchAsync(string term, CancellationToken cancellationToken = default);
    Task<ServiceResult<WatchlistMovieDto>> AddAsync(string imdbId, CancellationToken cancellationToken = default);
    Task<ServiceResult<WatchlistMovieDto>> SetWatchedAsync(Guid id, bool watched, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class WatchlistService : IWatchlistService
{
    private readonly IDbContextFactory<LoveLetterDbContext> _dbContextFactory;
    private readonly HttpClient _httpClient;
    private readonly IConfiguration _configuration;

    public WatchlistService(
        IDbContextFactory<LoveLetterDbContext> dbContextFactory,
        HttpClient httpClient,
        IConfiguration configuration)
    {
        _dbContextFactory = dbContextFactory;
        _httpClient = httpClient;
        _configuration = configuration;
    }

    public async Task<ServiceResult<IReadOnlyList<WatchlistMovieDto>>> GetAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var items = await db.WatchlistMovies
            .AsNoTracking()
            .OrderBy(m => m.Watched)
            .ThenByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

        return ServiceResult<IReadOnlyList<WatchlistMovieDto>>.Ok(items.Select(m => m.ToDto()).ToList());
    }

    public async Task<ServiceResult<IReadOnlyList<WatchlistSearchResult>>> SearchAsync(string term, CancellationToken cancellationToken = default)
    {
        var searchTerm = term?.Trim();
        if (string.IsNullOrWhiteSpace(searchTerm) || searchTerm.Length < 2)
        {
            return ServiceResult<IReadOnlyList<WatchlistSearchResult>>.Fail("Bitte mindestens zwei Zeichen eingeben.");
        }

        if (!TryGetApiKey(out var apiKey, out var error))
        {
            return ServiceResult<IReadOnlyList<WatchlistSearchResult>>.Fail(error);
        }

        var url = $"https://www.omdbapi.com/?apikey={Uri.EscapeDataString(apiKey)}&s={Uri.EscapeDataString(searchTerm)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ServiceResult<IReadOnlyList<WatchlistSearchResult>>.Fail("OMDB Anfrage fehlgeschlagen.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OmdbSearchResponse>(cancellationToken: cancellationToken);
        if (payload is null || !payload.IsSuccess)
        {
            var message = payload?.Error ?? "Keine Ergebnisse gefunden.";
            return ServiceResult<IReadOnlyList<WatchlistSearchResult>>.Fail(message);
        }

        var results = payload.Search?
            .Where(item => IsSupportedType(item.Type))
            .Select(item => new WatchlistSearchResult
            {
                ImdbId = item.ImdbId,
                Title = item.Title,
                Year = item.Year,
                PosterUrl = NormalizePoster(item.Poster),
                Type = NormalizeType(item.Type)
            })
            .ToList() ?? new List<WatchlistSearchResult>();

        if (results.Count == 0)
        {
            return ServiceResult<IReadOnlyList<WatchlistSearchResult>>.Fail("Keine Filme gefunden.");
        }

        return ServiceResult<IReadOnlyList<WatchlistSearchResult>>.Ok(results);
    }

    public async Task<ServiceResult<WatchlistMovieDto>> AddAsync(string imdbId, CancellationToken cancellationToken = default)
    {
        var normalizedId = imdbId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedId))
        {
            return ServiceResult<WatchlistMovieDto>.Fail("IMDB Id fehlt.");
        }

        if (!TryGetApiKey(out var apiKey, out var error))
        {
            return ServiceResult<WatchlistMovieDto>.Fail(error);
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.WatchlistMovies.AnyAsync(m => m.ImdbId == normalizedId, cancellationToken);
        if (exists)
        {
            return ServiceResult<WatchlistMovieDto>.Fail("Dieser Film steht bereits auf der Watchlist.");
        }

        var url = $"https://www.omdbapi.com/?apikey={Uri.EscapeDataString(apiKey)}&i={Uri.EscapeDataString(normalizedId)}";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return ServiceResult<WatchlistMovieDto>.Fail("Film konnte nicht geladen werden.");
        }

        var payload = await response.Content.ReadFromJsonAsync<OmdbMovieResponse>(cancellationToken: cancellationToken);
        if (payload is null || !payload.IsSuccess || string.IsNullOrWhiteSpace(payload.Title))
        {
            var message = payload?.Error ?? "Film nicht gefunden.";
            return ServiceResult<WatchlistMovieDto>.Fail(message);
        }

        var movie = new WatchlistMovie
        {
            Id = Guid.NewGuid(),
            ImdbId = payload.ImdbId ?? normalizedId,
            Title = payload.Title.Trim(),
            Year = payload.Year,
            PosterUrl = NormalizePoster(payload.Poster),
            Type = NormalizeType(payload.Type),
            Plot = NormalizePlot(payload.Plot),
            CreatedAt = DateTime.UtcNow,
            Watched = false
        };

        db.WatchlistMovies.Add(movie);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<WatchlistMovieDto>.Ok(movie.ToDto());
    }

    public async Task<ServiceResult<WatchlistMovieDto>> SetWatchedAsync(Guid id, bool watched, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.WatchlistMovies.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity is null)
        {
            return ServiceResult<WatchlistMovieDto>.Fail("Film nicht gefunden.");
        }

        entity.Watched = watched;
        entity.WatchedAt = watched ? DateTime.UtcNow : null;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<WatchlistMovieDto>.Ok(entity.ToDto());
    }

    public async Task<ServiceResult<bool>> DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entity = await db.WatchlistMovies.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
        if (entity is null)
        {
            return ServiceResult<bool>.Fail("Film nicht gefunden.");
        }

        db.WatchlistMovies.Remove(entity);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private bool TryGetApiKey(out string apiKey, out string error)
    {
        apiKey = _configuration[LoveConfigLoader.Keys.OmdbApiKey]
                 ?? Environment.GetEnvironmentVariable(LoveConfigLoader.Keys.OmdbApiKey)
                 ?? Environment.GetEnvironmentVariable("OMDB_API_KEY")
                 ?? string.Empty;

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            error = "OMDB API Key fehlt. Setze die Umgebungsvariable.";
            return false;
        }

        error = string.Empty;
        return true;
    }

    private static string? NormalizePoster(string? poster)
    {
        if (string.IsNullOrWhiteSpace(poster) || string.Equals(poster, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return poster;
    }

    private static string? NormalizePlot(string? plot)
    {
        if (string.IsNullOrWhiteSpace(plot) || string.Equals(plot, "N/A", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return plot.Trim();
    }

    private static string? NormalizeType(string? type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return null;
        }

        if (string.Equals(type, "movie", StringComparison.OrdinalIgnoreCase))
        {
            return "movie";
        }

        if (string.Equals(type, "series", StringComparison.OrdinalIgnoreCase))
        {
            return "series";
        }

        return type.Trim();
    }

    private static bool IsSupportedType(string? type)
    {
        return string.Equals(type, "movie", StringComparison.OrdinalIgnoreCase)
               || string.Equals(type, "series", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OmdbSearchResponse
    {
        public List<OmdbSearchItem>? Search { get; init; }
        public string? Response { get; init; }
        public string? Error { get; init; }

        public bool IsSuccess => string.Equals(Response, "True", StringComparison.OrdinalIgnoreCase);
    }

    private sealed class OmdbSearchItem
    {
        public string Title { get; init; } = string.Empty;
        public string Year { get; init; } = string.Empty;
        public string ImdbId { get; init; } = string.Empty;
        public string Type { get; init; } = string.Empty;
        public string Poster { get; init; } = string.Empty;
    }

    private sealed class OmdbMovieResponse
    {
        public string? Title { get; init; }
        public string? Year { get; init; }
        public string? Poster { get; init; }
        public string? ImdbId { get; init; }
        public string? Type { get; init; }
        public string? Plot { get; init; }
        public string? Response { get; init; }
        public string? Error { get; init; }

        public bool IsSuccess => string.Equals(Response, "True", StringComparison.OrdinalIgnoreCase);
    }
}
