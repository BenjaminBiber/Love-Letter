using System.Net.Http.Json;
using System.Text.Json;

namespace LoveLetter.App.Services;

public interface ICountryCatalogService
{
    Task<IReadOnlyList<CountryOption>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<CountryOption?> FindByCodeAsync(string? code, CancellationToken cancellationToken = default);
}

public sealed class CountryCatalogService : ICountryCatalogService
{
    private const string ApiUrl = "https://restcountries.com/v3.1/all?fields=name,flags,cca3";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _cachePath;
    private readonly object _lock = new();
    private IReadOnlyList<CountryOption>? _cache;

    public CountryCatalogService(IHttpClientFactory httpClientFactory, IWebHostEnvironment environment)
    {
        _httpClientFactory = httpClientFactory;
        var dataDir = Path.Combine(environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        _cachePath = Path.Combine(dataDir, "countries-cache.json");
    }

    public async Task<IReadOnlyList<CountryOption>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        if (_cache is not null)
        {
            return _cache;
        }

        var loaded = await LoadFromDiskAsync(cancellationToken);
        if (loaded is not null)
        {
            lock (_lock)
            {
                _cache ??= loaded;
            }
            return _cache;
        }

        var fetched = await FetchFromApiAsync(cancellationToken);
        lock (_lock)
        {
            _cache ??= fetched;
        }

        await SaveToDiskAsync(_cache, cancellationToken);
        return _cache;
    }

    public async Task<CountryOption?> FindByCodeAsync(string? code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return null;
        }

        var countries = await GetAllAsync(cancellationToken);
        return countries.FirstOrDefault(c => c.Code.Equals(code.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private async Task<IReadOnlyList<CountryOption>?> LoadFromDiskAsync(CancellationToken cancellationToken)
    {
        try
        {
            if (!File.Exists(_cachePath))
            {
                return null;
            }

            await using var stream = File.OpenRead(_cachePath);
            var cached = await JsonSerializer.DeserializeAsync<List<CountryOption>>(stream, cancellationToken: cancellationToken);
            if (cached is null || cached.Count == 0)
            {
                return null;
            }

            return cached;
        }
        catch
        {
            return null;
        }
    }

    private async Task SaveToDiskAsync(IReadOnlyList<CountryOption> countries, CancellationToken cancellationToken)
    {
        try
        {
            await using var stream = File.Create(_cachePath);
            await JsonSerializer.SerializeAsync(stream, countries, cancellationToken: cancellationToken);
        }
        catch
        {
            // ignore
        }
    }

    private async Task<IReadOnlyList<CountryOption>> FetchFromApiAsync(CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient();
        var response = await client.GetAsync(ApiUrl, cancellationToken);
        response.EnsureSuccessStatusCode();

        var countries = await response.Content.ReadFromJsonAsync<List<CountryApiResult>>(cancellationToken: cancellationToken)
            ?? new List<CountryApiResult>();

        return countries
            .Where(c => !string.IsNullOrWhiteSpace(c.Cca3) && c.Name?.Common is not null)
            .Select(c =>
            {
                var code = c.Cca3!.ToUpperInvariant();
                var name = c.Name!.Common ?? code;
                return new CountryOption(code, name, c.Flags?.Png, c.Flags?.Svg);
            })
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record CountryApiResult(CountryName? Name, CountryFlags? Flags, string? Cca3);
    private sealed record CountryName(string? Common);
    private sealed record CountryFlags(string? Png, string? Svg);
}

public sealed record CountryOption(string Code, string Name, string? FlagPng, string? FlagSvg);
