using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using SpotifyAPI.Web;

namespace LoveLetter.App.Services;

public interface ISpotifyPlaylistService
{
    Task<SpotifyPlaylistResult?> GetPlaylistAsync(CancellationToken cancellationToken = default);
}

public sealed class SpotifyPlaylistService : ISpotifyPlaylistService
{
    private readonly IMemoryCache _cache;
    private readonly ILogger<SpotifyPlaylistService> _logger;

    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
    };

    public SpotifyPlaylistService(IMemoryCache cache, ILogger<SpotifyPlaylistService> logger)
    {
        _cache = cache;
        _logger = logger;
    }

    public async Task<SpotifyPlaylistResult?> GetPlaylistAsync(CancellationToken cancellationToken = default)
    {
        var clientId = Environment.GetEnvironmentVariable(Configuration.LoveConfigLoader.Keys.SpotifyClientId)
                        ?? Environment.GetEnvironmentVariable("ClientId");
        var clientSecret = Environment.GetEnvironmentVariable(Configuration.LoveConfigLoader.Keys.SpotifyClientSecret)
                           ?? Environment.GetEnvironmentVariable("ClientSecret");
        var playlistId = Environment.GetEnvironmentVariable(Configuration.LoveConfigLoader.Keys.SpotifyPlaylistId)
                         ?? Environment.GetEnvironmentVariable("PlayListId");

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret) || string.IsNullOrWhiteSpace(playlistId))
        {
            return null;
        }

        cancellationToken.ThrowIfCancellationRequested();

        var cacheKey = $"spotify:playlist:{playlistId}";
        if (_cache.TryGetValue(cacheKey, out SpotifyPlaylistResult? cached) && cached is not null)
        {
            return cached;
        }

        try
        {
            var config = SpotifyClientConfig.CreateDefault();
            var tokenResponse = await new OAuthClient(config).RequestToken(new ClientCredentialsRequest(clientId, clientSecret));
            var spotify = new SpotifyClient(config.WithToken(tokenResponse.AccessToken));

            var playlist = await spotify.Playlists.GetItems(playlistId);
            var playlistTracks = playlist.Items;

            if (playlistTracks is null || playlistTracks.Count == 0)
            {
                return null;
            }

            var tracks = playlistTracks
                .Select(item => item.Track)
                .OfType<FullTrack>()
                .ToList();

            if (tracks.Count == 0)
            {
                return null;
            }

            var playlistUrl = $"https://open.spotify.com/playlist/{playlistId}?si=2YT8t8gmTCaZYTaqCU0R3Q";
            var result = new SpotifyPlaylistResult(playlistId, playlistUrl, tracks);

            _cache.Set(cacheKey, result, CacheOptions);
            return result;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load Spotify playlist {PlaylistId}", playlistId);
            return null;
        }
    }
}

public sealed record SpotifyPlaylistResult(string PlaylistId, string PlaylistUrl, IReadOnlyList<FullTrack> Tracks);
