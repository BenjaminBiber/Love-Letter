using System.IO;
using LoveLetter.App.Data;
using LoveLetter.App.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace LoveLetter.App.Services;

public interface IGalleryService
{
    int FavoriteLimit { get; }
    Task<IReadOnlyList<GalleryPhotoDto>> GetPhotosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GalleryPhotoDto>> GetFavoritesAsync(int? limit = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryPhotoDto>> UploadPhotoAsync(UploadedMediaFile file, string? caption, CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryPhotoDto>> SetFavoriteStateAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryPhotoDto>> UpdatePhotoAsync(Guid id, string? caption, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class GalleryService : IGalleryService
{
    private const long MaxPhotoBytes = 15 * 1024 * 1024;
    private const int ThumbnailMaxSize = 512;
    private static readonly string[] AllowedPhotoExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    private readonly IDbContextFactory<LoveLetterDbContext> _dbContextFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly IImageThumbnailService _thumbnailService;
    private readonly int _favoriteLimit;

    public GalleryService(IDbContextFactory<LoveLetterDbContext> dbContextFactory, IWebHostEnvironment environment, IImageThumbnailService thumbnailService)
    {
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _thumbnailService = thumbnailService;
        _favoriteLimit = ResolveFavoriteLimit();
    }

    public int FavoriteLimit => _favoriteLimit;

    public async Task<IReadOnlyList<GalleryPhotoDto>> GetPhotosAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photos = await db.GalleryPhotos
            .AsNoTracking()
            .OrderByDescending(p => p.IsFavorite)
            .ThenByDescending(p => p.FavoritedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.CreatedAt)
            .ToListAsync(cancellationToken);

        return photos.Select(p => p.ToDto()).ToList();
    }

    public async Task<IReadOnlyList<GalleryPhotoDto>> GetFavoritesAsync(int? limit = null, CancellationToken cancellationToken = default)
    {
        var take = limit is > 0 ? limit.Value : FavoriteLimit;
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var favorites = await db.GalleryPhotos
            .AsNoTracking()
            .Where(p => p.IsFavorite)
            .OrderByDescending(p => p.FavoritedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        return favorites.Select(p => p.ToDto()).ToList();
    }

    public async Task<ServiceResult<GalleryPhotoDto>> UploadPhotoAsync(UploadedMediaFile file, string? caption, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte wähle ein Foto aus.");
        }

        if (file.Length > MaxPhotoBytes)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Das Foto ist zu groß (maximal 15 MB).");
        }

        if (!string.IsNullOrWhiteSpace(caption) && caption.Length > 160)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte verwende maximal 160 Zeichen für die Bildbeschreibung.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(extension))
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Nur PNG, JPG oder WEBP werden unterstützt.");
        }

        var webRoot = GetWebRoot();
        var galleryRoot = Path.Combine(webRoot, "uploads", "gallery");
        Directory.CreateDirectory(galleryRoot);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var destinationPath = Path.Combine(galleryRoot, fileName);
        await using var sourceStream = await file.OpenReadStreamAsync(cancellationToken);
        await using var destinationStream = File.Create(destinationPath);
        await sourceStream.CopyToAsync(destinationStream, cancellationToken);

        var relativePath = Path.Combine("uploads", "gallery", fileName).Replace("\\", "/");
        var thumbnailRelativePath = await GenerateThumbnailRelativePathAsync(destinationPath, galleryRoot, fileName, cancellationToken)
            ?? relativePath;

        var photo = new GalleryPhoto
        {
            Id = Guid.NewGuid(),
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim(),
            FilePath = relativePath,
            ThumbnailPath = thumbnailRelativePath,
            OriginalFileName = file.FileName,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        db.GalleryPhotos.Add(photo);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<GalleryPhotoDto>.Ok(photo.ToDto());
    }

    public async Task<ServiceResult<GalleryPhotoDto>> SetFavoriteStateAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Foto nicht gefunden.");
        }

        if (photo.IsFavorite == isFavorite)
        {
            return ServiceResult<GalleryPhotoDto>.Ok(photo.ToDto());
        }

        if (isFavorite)
        {
            var favoriteCount = await db.GalleryPhotos.CountAsync(p => p.IsFavorite, cancellationToken);
            if (favoriteCount >= FavoriteLimit)
            {
                return ServiceResult<GalleryPhotoDto>.Fail($"Du kannst maximal {FavoriteLimit} Favoriten markieren.");
            }

            photo.IsFavorite = true;
            photo.FavoritedAt = DateTime.UtcNow;
        }
        else
        {
            photo.IsFavorite = false;
            photo.FavoritedAt = null;
        }

        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<GalleryPhotoDto>.Ok(photo.ToDto());
    }

    public async Task<ServiceResult<GalleryPhotoDto>> UpdatePhotoAsync(Guid id, string? caption, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(caption) && caption.Length > 160)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte maximal 160 Zeichen verwenden.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Foto nicht gefunden.");
        }

        photo.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        await db.SaveChangesAsync(cancellationToken);
        return ServiceResult<GalleryPhotoDto>.Ok(photo.ToDto());
    }

    public async Task<ServiceResult<bool>> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<bool>.Fail("Foto nicht gefunden.");
        }

        db.GalleryPhotos.Remove(photo);
        await db.SaveChangesAsync(cancellationToken);

        TryDeleteFile(photo.FilePath);
        TryDeleteFile(photo.ThumbnailPath);

        return ServiceResult<bool>.Ok(true);
    }

    private string GetWebRoot()
    {
        return string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
    }

    private int ResolveFavoriteLimit()
    {
        var raw = Environment.GetEnvironmentVariable(Configuration.LoveConfigLoader.Keys.GalleryFavoriteLimit);
        if (int.TryParse(raw, out var parsed) && parsed > 0)
        {
            return parsed;
        }

        return 6;
    }

    internal async Task<string?> GenerateThumbnailRelativePathAsync(string sourcePath, string baseDirectory, string fileName, CancellationToken cancellationToken)
    {
        var thumbnailDirectory = Path.Combine(baseDirectory, "thumbs");
        var thumbnailFullPath = await _thumbnailService.GenerateThumbnailAsync(
            sourcePath,
            thumbnailDirectory,
            Path.GetFileNameWithoutExtension(fileName),
            ThumbnailMaxSize,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(thumbnailFullPath))
        {
            return null;
        }

        var webRoot = GetWebRoot();
        return NormalizeRelativePath(webRoot, thumbnailFullPath);
    }

    internal static string NormalizeRelativePath(string webRoot, string absolutePath)
    {
        var relative = Path.GetRelativePath(webRoot, absolutePath);
        return relative.Replace("\\", "/");
    }

    private void TryDeleteFile(string? relativePath)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return;
        }

        try
        {
            var webRoot = GetWebRoot();
            var sanitized = relativePath.TrimStart('/', '\\');
            var path = Path.Combine(webRoot, sanitized);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch
        {
            // ignore cleanup failures
        }
    }
}
