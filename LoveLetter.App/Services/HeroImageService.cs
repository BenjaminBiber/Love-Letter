using System.Text.Json;
using System.IO;
using LoveLetter.App.Configuration;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Components.Forms;
using Microsoft.Extensions.Logging;

namespace LoveLetter.App.Services;

public interface IHeroImageService
{
    Task<HeroImageInfo> GetAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<HeroImageInfo>> UploadAsync(IBrowserFile file, string? caption, CancellationToken cancellationToken = default);
    Task<ServiceResult<HeroImageInfo>> UpdateCaptionAsync(string? caption, CancellationToken cancellationToken = default);
}

public sealed class HeroImageService : IHeroImageService
{
    private const long MaxHeroBytes = 15 * 1024 * 1024;
    private static readonly string[] AllowedExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly IWebHostEnvironment _environment;
    private readonly ILogger<HeroImageService> _logger;

    public HeroImageService(IWebHostEnvironment environment, ILogger<HeroImageService> logger)
    {
        _environment = environment;
        _logger = logger;
    }

    public async Task<HeroImageInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        var fallback = LoveContent.Value.Hero.FeaturedPhoto;
        var meta = await ReadMetadataAsync(cancellationToken);
        if (meta is null)
        {
            return new HeroImageInfo(fallback.Src, fallback.Caption);
        }

        var absolute = ToAbsolutePath(meta.Src);
        if (string.IsNullOrWhiteSpace(absolute) || !File.Exists(absolute))
        {
            return new HeroImageInfo(fallback.Src, meta.Caption ?? fallback.Caption);
        }

        return new HeroImageInfo(NormalizeRelative(meta.Src), meta.Caption);
    }

    public async Task<ServiceResult<HeroImageInfo>> UploadAsync(IBrowserFile file, string? caption, CancellationToken cancellationToken = default)
    {
        if (file is null || file.Size == 0)
        {
            return ServiceResult<HeroImageInfo>.Fail("Bitte eine Bilddatei auswaehlen.");
        }

        if (file.Size > MaxHeroBytes)
        {
            return ServiceResult<HeroImageInfo>.Fail("Das Bild ist zu gross (maximal 15 MB).");
        }

        var ext = Path.GetExtension(file.Name).ToLowerInvariant();
        if (!AllowedExtensions.Contains(ext))
        {
            return ServiceResult<HeroImageInfo>.Fail("Nur PNG, JPG, JPEG oder WEBP sind erlaubt.");
        }

        try
        {
            var uploadsDir = GetHeroUploadDirectory();
            Directory.CreateDirectory(uploadsDir);

            var fileName = $"hero-{DateTime.UtcNow:yyyyMMddHHmmss}{ext}";
            var destinationPath = Path.Combine(uploadsDir, fileName);

            await using var read = file.OpenReadStream(MaxHeroBytes);
            await using var write = File.Create(destinationPath);
            await read.CopyToAsync(write, cancellationToken);

            await DeletePreviousFileAsync(cancellationToken);

            var relative = NormalizeRelative(Path.Combine("uploads", "hero", fileName));
            var info = new HeroImageInfo(relative, caption?.Trim());
            await WriteMetadataAsync(new HeroImageMetadata { Src = relative, Caption = info.Caption }, cancellationToken);
            return ServiceResult<HeroImageInfo>.Ok(info);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hero image upload failed.");
            return ServiceResult<HeroImageInfo>.Fail("Upload fehlgeschlagen.");
        }
    }

    public async Task<ServiceResult<HeroImageInfo>> UpdateCaptionAsync(string? caption, CancellationToken cancellationToken = default)
    {
        try
        {
            var meta = await ReadMetadataAsync(cancellationToken);
            if (meta is null)
            {
                var fallback = LoveContent.Value.Hero.FeaturedPhoto;
                meta = new HeroImageMetadata { Src = fallback.Src, Caption = caption?.Trim() };
            }
            else
            {
                meta.Caption = caption?.Trim();
            }

            await WriteMetadataAsync(meta, cancellationToken);
            return ServiceResult<HeroImageInfo>.Ok(new HeroImageInfo(NormalizeRelative(meta.Src), meta.Caption));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to update hero caption.");
            return ServiceResult<HeroImageInfo>.Fail("Untertitel konnte nicht gespeichert werden.");
        }
    }

    private string GetHeroUploadDirectory()
    {
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        return Path.Combine(webRoot, "uploads", "hero");
    }

    private string GetMetadataPath()
    {
        var dataDir = Path.Combine(_environment.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dataDir);
        return Path.Combine(dataDir, "hero-image.json");
    }

    private async Task<HeroImageMetadata?> ReadMetadataAsync(CancellationToken cancellationToken)
    {
        try
        {
            var path = GetMetadataPath();
            if (!File.Exists(path))
            {
                return null;
            }

            var json = await File.ReadAllTextAsync(path, cancellationToken);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            return JsonSerializer.Deserialize<HeroImageMetadata>(json);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to read hero image metadata.");
            return null;
        }
    }

    private async Task WriteMetadataAsync(HeroImageMetadata meta, CancellationToken cancellationToken)
    {
        var path = GetMetadataPath();
        var json = JsonSerializer.Serialize(meta, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(path, json, cancellationToken);
    }

    private async Task DeletePreviousFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            var meta = await ReadMetadataAsync(cancellationToken);
            if (meta is null)
            {
                return;
            }

            var absolute = ToAbsolutePath(meta.Src);
            var relative = NormalizeRelative(meta.Src ?? string.Empty).TrimStart('/');
            if (!string.IsNullOrWhiteSpace(absolute) &&
                File.Exists(absolute) &&
                relative.StartsWith("uploads/hero", StringComparison.OrdinalIgnoreCase))
            {
                File.Delete(absolute);
            }
        }
        catch
        {
            // ignore cleanup
        }
    }

    private string? ToAbsolutePath(string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative))
        {
            return null;
        }

        var sanitized = relative.TrimStart('/', '\\');
        var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;

        return Path.Combine(webRoot, sanitized);
    }

    private static string NormalizeRelative(string? path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return string.Empty;
        }

        return path.Replace("\\", "/");
    }
}

public sealed record HeroImageInfo(string Src, string? Caption);

internal sealed class HeroImageMetadata
{
    public string Src { get; set; } = string.Empty;
    public string? Caption { get; set; }
}
