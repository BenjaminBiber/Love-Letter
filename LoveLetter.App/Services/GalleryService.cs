using System.IO;
using LoveLetter.App.Data;
using LoveLetter.App.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace LoveLetter.App.Services;

public interface IGalleryService
{
    int FavoriteLimit { get; }
    Task<IReadOnlyList<GalleryAlbumDto>> GetAlbumsAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryAlbumDto>> CreateAlbumAsync(string name, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GalleryPhotoDto>> GetPhotosAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GalleryPhotoDto>> GetFavoritesAsync(int? limit = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryPhotoDto>> UploadPhotoAsync(UploadedMediaFile file, string? caption, string? album, CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryPhotoDto>> SetFavoriteStateAsync(Guid id, bool isFavorite, CancellationToken cancellationToken = default);
    Task<ServiceResult<GalleryPhotoDto>> UpdatePhotoAsync(Guid id, string? caption, string? album, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> DeletePhotoAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class GalleryService : IGalleryService
{
    private const long MaxPhotoBytes = 15 * 1024 * 1024;
    private const int ThumbnailMaxSize = 512;
    private const int MaxAlbumLength = 80;
    private const string FavoritesAlbumName = "Favoriten";
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

    public async Task<IReadOnlyList<GalleryAlbumDto>> GetAlbumsAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var albumEntities = await db.GalleryAlbums.AsNoTracking().ToListAsync(cancellationToken);
        var photos = await db.GalleryPhotos.AsNoTracking().ToListAsync(cancellationToken);

        var result = new List<GalleryAlbumDto>();
        var albumLookup = albumEntities.ToDictionary(a => a.Name, StringComparer.OrdinalIgnoreCase);

        foreach (var album in albumEntities.OrderBy(a => a.CreatedAt))
        {
            var albumPhotos = photos.Where(p => string.Equals(p.Album, album.Name, StringComparison.OrdinalIgnoreCase)).ToList();
            var cover = albumPhotos.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            result.Add(new GalleryAlbumDto
            {
                Id = album.Id,
                Name = album.Name,
                PhotoCount = albumPhotos.Count,
                CoverUrl = cover?.PhotoUrl,
                CoverThumbnailUrl = cover?.ThumbnailUrl,
                IsFavorite = false,
                IsUnassigned = false
            });
        }

        var photoAlbumNames = photos
            .Where(p => !string.IsNullOrWhiteSpace(p.Album))
            .Select(p => p.Album!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in photoAlbumNames)
        {
            if (albumLookup.ContainsKey(name))
            {
                continue;
            }

            var albumPhotos = photos.Where(p => string.Equals(p.Album, name, StringComparison.OrdinalIgnoreCase)).ToList();
            var cover = albumPhotos.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            result.Add(new GalleryAlbumDto
            {
                Id = null,
                Name = name,
                PhotoCount = albumPhotos.Count,
                CoverUrl = cover?.PhotoUrl,
                CoverThumbnailUrl = cover?.ThumbnailUrl,
                IsFavorite = false,
                IsUnassigned = false
            });
        }

        var unassigned = photos.Where(p => string.IsNullOrWhiteSpace(p.Album)).ToList();
        if (unassigned.Count > 0)
        {
            var cover = unassigned.OrderByDescending(p => p.CreatedAt).FirstOrDefault();
            result.Add(GalleryAlbumDto.FromUnassigned(unassigned.Count, cover));
        }

        var favorites = photos.Where(p => p.IsFavorite).ToList();
        if (favorites.Count > 0)
        {
            var cover = favorites.OrderByDescending(p => p.FavoritedAt ?? p.CreatedAt).FirstOrDefault();
            result.Insert(0, GalleryAlbumDto.FromFavorite(FavoritesAlbumName, favorites.Count, cover));
        }

        return result
            .OrderBy(a => a.IsFavorite ? 0 : a.IsUnassigned ? 2 : 1)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ServiceResult<GalleryAlbumDto>> CreateAlbumAsync(string name, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeAlbum(name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return ServiceResult<GalleryAlbumDto>.Fail("Albumnamen bitte ausfuellen.");
        }

        if (normalized.Length > MaxAlbumLength)
        {
            return ServiceResult<GalleryAlbumDto>.Fail("Bitte maximal 80 Zeichen fuer den Albumnamen verwenden.");
        }

        if (IsReservedAlbumName(normalized))
        {
            return ServiceResult<GalleryAlbumDto>.Fail("Dieser Albumnamen ist reserviert.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var exists = await db.GalleryAlbums.AnyAsync(a => a.Name.ToLower() == normalized.ToLower(), cancellationToken);
        var photoUsesName = await db.GalleryPhotos.AnyAsync(p => p.Album != null && p.Album.ToLower() == normalized.ToLower(), cancellationToken);

        if (exists || photoUsesName)
        {
            return ServiceResult<GalleryAlbumDto>.Fail("Ein Album mit diesem Namen existiert bereits.");
        }

        var album = new GalleryAlbum
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            CreatedAt = DateTime.UtcNow
        };

        db.GalleryAlbums.Add(album);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<GalleryAlbumDto>.Ok(new GalleryAlbumDto
        {
            Id = album.Id,
            Name = album.Name,
            PhotoCount = 0,
            CoverUrl = null,
            CoverThumbnailUrl = null,
            IsFavorite = false,
            IsUnassigned = false
        });
    }

    public async Task<IReadOnlyList<GalleryPhotoDto>> GetPhotosAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photos = await db.GalleryPhotos
            .AsNoTracking()
            .OrderByDescending(p => p.IsFavorite)
            .ThenBy(p => string.IsNullOrWhiteSpace(p.Album))
            .ThenBy(p => p.Album)
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

    public async Task<ServiceResult<GalleryPhotoDto>> UploadPhotoAsync(UploadedMediaFile file, string? caption, string? album, CancellationToken cancellationToken = default)
    {
        if (file.Length == 0)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte waehle ein Foto aus.");
        }

        if (file.Length > MaxPhotoBytes)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Das Foto ist zu gross (maximal 15 MB).");
        }

        if (!string.IsNullOrWhiteSpace(caption) && caption.Length > 160)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte verwende maximal 160 Zeichen fuer die Bildbeschreibung.");
        }

        var normalizedAlbum = NormalizeAlbum(album);
        if (normalizedAlbum is { Length: > MaxAlbumLength })
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte maximal 80 Zeichen fuer den Albumnamen verwenden.");
        }

        if (normalizedAlbum is not null && IsReservedAlbumName(normalizedAlbum))
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Dieser Albumnamen ist reserviert.");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(extension))
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Nur PNG, JPG oder WEBP werden unterstuetzt.");
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
            Album = normalizedAlbum,
            FilePath = relativePath,
            ThumbnailPath = thumbnailRelativePath,
            OriginalFileName = file.FileName,
            CreatedAt = DateTime.UtcNow
        };

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        if (normalizedAlbum is not null)
        {
            await EnsureAlbumExistsAsync(db, normalizedAlbum, cancellationToken);
        }

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

    public async Task<ServiceResult<GalleryPhotoDto>> UpdatePhotoAsync(Guid id, string? caption, string? album, CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(caption) && caption.Length > 160)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte maximal 160 Zeichen verwenden.");
        }

        var normalizedAlbum = NormalizeAlbum(album);
        if (normalizedAlbum is { Length: > MaxAlbumLength })
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Bitte maximal 80 Zeichen fuer den Albumnamen verwenden.");
        }

        if (normalizedAlbum is not null && IsReservedAlbumName(normalizedAlbum))
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Dieser Albumnamen ist reserviert.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photo = await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
        if (photo is null)
        {
            return ServiceResult<GalleryPhotoDto>.Fail("Foto nicht gefunden.");
        }

        photo.Caption = string.IsNullOrWhiteSpace(caption) ? null : caption.Trim();
        photo.Album = normalizedAlbum;
        if (normalizedAlbum is not null)
        {
            await EnsureAlbumExistsAsync(db, normalizedAlbum, cancellationToken);
        }

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

    private static string? NormalizeAlbum(string? album)
    {
        if (string.IsNullOrWhiteSpace(album))
        {
            return null;
        }

        return album.Trim();
    }

    private static bool IsReservedAlbumName(string album)
    {
        return string.Equals(album, GalleryPhoto.UnassignedAlbumName, StringComparison.OrdinalIgnoreCase)
               || string.Equals(album, FavoritesAlbumName, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureAlbumExistsAsync(LoveLetterDbContext db, string album, CancellationToken cancellationToken)
    {
        var exists = await db.GalleryAlbums.AnyAsync(a => a.Name.ToLower() == album.ToLower(), cancellationToken);
        if (!exists)
        {
            db.GalleryAlbums.Add(new GalleryAlbum
            {
                Id = Guid.NewGuid(),
                Name = album,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync(cancellationToken);
        }
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
