using System.IO.Compression;
using LoveLetter.App.Data;
using LoveLetter.App.Models;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LoveLetter.App.Endpoints;

public static class GalleryEndpoints
{
    private const long MaxPhotoBytes = 15 * 1024 * 1024; // 15 MB
    private const int MaxCaptionLength = 160;
    private const int MaxAlbumLength = 80;
    private const int FavoriteLimit = 6;
    private static readonly string[] AllowedPhotoExtensions = [".png", ".jpg", ".jpeg", ".webp"];

    public static RouteGroupBuilder MapGalleryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/gallery");
        group.MapGet("/", GetPhotosAsync);
        group.MapGet("/favorites", GetFavoritesAsync);
        group.MapGet("/albums", GetAlbumsAsync);
        group.MapPost("/albums", CreateAlbumAsync);
        group.MapGet("/export", ExportPhotosAsync);
        group.MapPost("/", UploadPhotoAsync);
        group.MapPost("/{id:guid}/favorite", SetFavoriteAsync);
        return group;
    }

    private static async Task<Ok<List<GalleryPhotoDto>>> GetPhotosAsync(LoveLetterDbContext db)
    {
        var items = await db.GalleryPhotos
            .AsNoTracking()
            .OrderByDescending(p => p.IsFavorite)
            .ThenBy(p => string.IsNullOrWhiteSpace(p.Album))
            .ThenBy(p => p.Album)
            .ThenByDescending(p => p.FavoritedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Select(p => p.ToDto())
            .ToListAsync();

        return TypedResults.Ok(items);
    }

    private static async Task<Ok<List<GalleryPhotoDto>>> GetFavoritesAsync(LoveLetterDbContext db)
    {
        var favorites = await db.GalleryPhotos
            .AsNoTracking()
            .Where(p => p.IsFavorite)
            .OrderByDescending(p => p.FavoritedAt ?? p.CreatedAt)
            .ThenByDescending(p => p.CreatedAt)
            .Take(FavoriteLimit)
            .Select(p => p.ToDto())
            .ToListAsync();

        return TypedResults.Ok(favorites);
    }

    private static async Task<Ok<List<GalleryAlbumDto>>> GetAlbumsAsync(LoveLetterDbContext db)
    {
        var photos = await db.GalleryPhotos.AsNoTracking().ToListAsync();
        var albums = await db.GalleryAlbums.AsNoTracking().OrderBy(a => a.CreatedAt).ToListAsync();

        var result = new List<GalleryAlbumDto>();
        foreach (var album in albums)
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

        var usedAlbumNames = new HashSet<string>(albums.Select(a => a.Name), StringComparer.OrdinalIgnoreCase);
        var photoAlbumNames = photos
            .Where(p => !string.IsNullOrWhiteSpace(p.Album))
            .Select(p => p.Album!)
            .Distinct(StringComparer.OrdinalIgnoreCase);

        foreach (var name in photoAlbumNames)
        {
            if (usedAlbumNames.Contains(name))
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
            result.Insert(0, GalleryAlbumDto.FromFavorite("Favoriten", favorites.Count, cover));
        }

        var ordered = result
            .OrderBy(a => a.IsFavorite ? 0 : a.IsUnassigned ? 2 : 1)
            .ThenBy(a => a.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return TypedResults.Ok(ordered);
    }

    private static async Task<IResult> CreateAlbumAsync(CreateAlbumRequest request, LoveLetterDbContext db)
    {
        var normalized = NormalizeAlbum(request.Name);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return TypedResults.BadRequest("Albumnamen bitte ausfuellen.");
        }

        if (normalized.Length > MaxAlbumLength)
        {
            return TypedResults.BadRequest("Bitte maximal 80 Zeichen fuer den Albumnamen verwenden.");
        }

        if (IsReservedAlbumName(normalized))
        {
            return TypedResults.BadRequest("Dieser Albumnamen ist reserviert.");
        }

        var exists = await db.GalleryAlbums.AnyAsync(a => a.Name.ToLower() == normalized.ToLower());
        var photoUsesName = await db.GalleryPhotos.AnyAsync(p => p.Album != null && p.Album.ToLower() == normalized.ToLower());

        if (exists || photoUsesName)
        {
            return TypedResults.BadRequest("Ein Album mit diesem Namen existiert bereits.");
        }

        var album = new GalleryAlbum
        {
            Id = Guid.NewGuid(),
            Name = normalized,
            CreatedAt = DateTime.UtcNow
        };

        db.GalleryAlbums.Add(album);
        await db.SaveChangesAsync();

        return TypedResults.Ok(new GalleryAlbumDto
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

    private static async Task<IResult> UploadPhotoAsync(
        HttpRequest request,
        LoveLetterDbContext db,
        IWebHostEnvironment env)
    {
        if (!request.HasFormContentType)
        {
            return TypedResults.BadRequest("Ungueltiges Formular.");
        }

        var form = await request.ReadFormAsync();
        var file = form.Files["photo"];
        var captionValue = form["caption"].ToString();
        var caption = string.IsNullOrWhiteSpace(captionValue) ? null : captionValue.Trim();
        var albumValue = form["album"].ToString();
        var album = NormalizeAlbum(albumValue);

        if (file is null || file.Length == 0)
        {
            return TypedResults.BadRequest("Bitte waehle ein Foto aus.");
        }

        if (caption is not null && caption.Length > MaxCaptionLength)
        {
            return TypedResults.BadRequest($"Bitte verwende maximal {MaxCaptionLength} Zeichen fuer die Bildbeschreibung.");
        }

        if (album is not null && album.Length > MaxAlbumLength)
        {
            return TypedResults.BadRequest($"Bitte verwende maximal {MaxAlbumLength} Zeichen fuer den Albumnamen.");
        }

        if (album is not null && IsReservedAlbumName(album))
        {
            return TypedResults.BadRequest("Dieser Albumnamen ist reserviert.");
        }

        if (file.Length > MaxPhotoBytes)
        {
            return TypedResults.BadRequest("Das Foto ist zu gross (maximal 15 MB).");
        }

        var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
        if (!AllowedPhotoExtensions.Contains(extension))
        {
            return TypedResults.BadRequest("Nur PNG, JPG oder WEBP werden unterstuetzt.");
        }

        var fileName = $"{Guid.NewGuid()}{extension}";
        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
        var uploadRoot = Path.Combine(webRoot, "uploads", "gallery");
        Directory.CreateDirectory(uploadRoot);
        var relativePath = Path.Combine("uploads", "gallery", fileName).Replace("\\", "/");
        var fullPath = Path.Combine(uploadRoot, fileName);

        await using (var stream = File.Create(fullPath))
        {
            await file.CopyToAsync(stream);
        }

        var photo = new GalleryPhoto
        {
            Id = Guid.NewGuid(),
            Caption = string.IsNullOrWhiteSpace(caption) ? null : caption,
            Album = album,
            FilePath = relativePath,
            OriginalFileName = file.FileName,
            CreatedAt = DateTime.UtcNow
        };

        if (album is not null)
        {
            await EnsureAlbumExistsAsync(db, album);
        }

        db.GalleryPhotos.Add(photo);
        await db.SaveChangesAsync();

        return TypedResults.Ok(photo.ToDto());
    }

    private static async Task<IResult> SetFavoriteAsync(
        Guid id,
        SetGalleryFavoriteRequest request,
        LoveLetterDbContext db)
    {
        var photo = await db.GalleryPhotos.FirstOrDefaultAsync(p => p.Id == id);
        if (photo is null)
        {
            return TypedResults.NotFound();
        }

        if (photo.IsFavorite == request.IsFavorite)
        {
            return TypedResults.Ok(photo.ToDto());
        }

        if (request.IsFavorite)
        {
            var currentFavorites = await db.GalleryPhotos.CountAsync(p => p.IsFavorite);
            if (currentFavorites >= FavoriteLimit)
            {
                return TypedResults.BadRequest($"Du kannst maximal {FavoriteLimit} Favoriten markieren.");
            }

            photo.IsFavorite = true;
            photo.FavoritedAt = DateTime.UtcNow;
        }
        else
        {
            photo.IsFavorite = false;
            photo.FavoritedAt = null;
        }

        await db.SaveChangesAsync();

        return TypedResults.Ok(photo.ToDto());
    }

    private static async Task<IResult> ExportPhotosAsync(
        [FromQuery(Name = "ids")] Guid[] ids,
        LoveLetterDbContext db,
        IWebHostEnvironment env,
        CancellationToken cancellationToken)
    {
        if (ids is null || ids.Length == 0)
        {
            return TypedResults.BadRequest("Bitte waehle mindestens ein Foto aus.");
        }

        var requestedIds = ids.Distinct().ToArray();
        var photos = await db.GalleryPhotos
            .AsNoTracking()
            .Where(p => requestedIds.Contains(p.Id))
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return TypedResults.NotFound("Keine passenden Fotos gefunden.");
        }

        var photoLookup = photos.ToDictionary(p => p.Id);
        var orderedPhotos = requestedIds
            .Where(photoLookup.ContainsKey)
            .Select(id => photoLookup[id])
            .ToList();

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var archiveStream = new MemoryStream();
        var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using (var archive = new ZipArchive(archiveStream, ZipArchiveMode.Create, true))
        {
            foreach (var photo in orderedPhotos)
            {
                var physicalPath = ResolvePhotoPath(webRoot, photo.FilePath);
                if (physicalPath is null || !File.Exists(physicalPath))
                {
                    continue;
                }

                var entryName = CreateEntryName(photo, usedNames);
                var entry = archive.CreateEntry(entryName, CompressionLevel.NoCompression);
                await using var entryStream = entry.Open();
                await using var fileStream = File.OpenRead(physicalPath);
                await fileStream.CopyToAsync(entryStream, cancellationToken);
            }
        }

        if (archiveStream.Length == 0)
        {
            await archiveStream.DisposeAsync();
            return TypedResults.BadRequest("Es konnten keine Dateien exportiert werden.");
        }

        archiveStream.Seek(0, SeekOrigin.Begin);
        var downloadName = $"gallery-export-{DateTime.UtcNow:yyyyMMdd-HHmmss}.zip";
        return TypedResults.File(archiveStream, "application/zip", downloadName);
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
               || string.Equals(album, "Favoriten", StringComparison.OrdinalIgnoreCase);
    }

    private static async Task EnsureAlbumExistsAsync(LoveLetterDbContext db, string album)
    {
        var exists = await db.GalleryAlbums.AnyAsync(a => a.Name.ToLower() == album.ToLower());
        if (!exists)
        {
            db.GalleryAlbums.Add(new GalleryAlbum
            {
                Id = Guid.NewGuid(),
                Name = album,
                CreatedAt = DateTime.UtcNow
            });
            await db.SaveChangesAsync();
        }
    }

    private static string? ResolvePhotoPath(string webRoot, string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return null;
        }

        var trimmed = filePath.TrimStart('/', '\\');
        var normalized = trimmed.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return Path.Combine(webRoot, normalized);
    }

    private static string CreateEntryName(GalleryPhoto photo, HashSet<string> usedNames)
    {
        var original = !string.IsNullOrWhiteSpace(photo.OriginalFileName)
            ? Path.GetFileName(photo.OriginalFileName)
            : null;
        var fallbackExtension = Path.GetExtension(photo.FilePath);
        var baseName = string.IsNullOrWhiteSpace(original)
            ? $"Foto-{photo.CreatedAt:yyyyMMdd-HHmmss}{fallbackExtension}"
            : original;

        var name = baseName;
        var duplicateIndex = 1;
        while (!usedNames.Add(name))
        {
            var withoutExtension = Path.GetFileNameWithoutExtension(baseName);
            var extension = Path.GetExtension(baseName);
            name = $"{withoutExtension}_{duplicateIndex}{extension}";
            duplicateIndex++;
        }

        return name;
    }

    private sealed record CreateAlbumRequest(string Name);
}
