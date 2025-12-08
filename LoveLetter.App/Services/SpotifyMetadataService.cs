using System.Net.Http.Json;
using System.Text.Json.Serialization;
using LoveLetter.App.Models;
using Microsoft.Extensions.Caching.Memory;

namespace LoveLetter.App.Services;

public interface ISpotifyMetadataService
{
    Task<SpotifyMetadata?> GetMetadataAsync(string trackUrl, CancellationToken cancellationToken = default);
}

public sealed class SpotifyMetadataService : ISpotifyMetadataService
{
    private readonly HttpClient _httpClient;
    private readonly IMemoryCache _cache;
    private static readonly MemoryCacheEntryOptions CacheOptions = new()
    {
        SlidingExpiration = TimeSpan.FromHours(6)
    };

    public SpotifyMetadataService(HttpClient httpClient, IMemoryCache cache)
    {
        _httpClient = httpClient;
        _cache = cache;
    }

    public async Task<SpotifyMetadata?> GetMetadataAsync(string trackUrl, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(trackUrl))
        {
            return null;
        }

        if (_cache.TryGetValue(trackUrl, out SpotifyMetadata? cached) && cached is not null)
        {
            return cached;
        }

        var endpoint = $"https://open.spotify.com/oembed?url={Uri.EscapeDataString(trackUrl)}";
        SpotifyOEmbedResponse? response;

        try
        {
            response = await _httpClient.GetFromJsonAsync<SpotifyOEmbedResponse>(endpoint, cancellationToken);
        }
        catch (HttpRequestException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
        catch (System.Text.Json.JsonException)
        {
            return null;
        }

        if (response is null)
        {
            return null;
        }

        var metadata = new SpotifyMetadata(response.ThumbnailUrl, response.Title);
        _cache.Set(trackUrl, metadata, CacheOptions);
        return metadata;
    }

    private sealed record SpotifyOEmbedResponse(
        [property: JsonPropertyName("thumbnail_url")] string? ThumbnailUrl,
        [property: JsonPropertyName("title")] string? Title);
}
