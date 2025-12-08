using LoveLetter.App.Data;
using LoveLetter.App.Models;
using LoveLetter.App.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BucketListSecurityOptions = LoveLetter.App.Configuration.BucketListSecurityOptions;

namespace LoveLetter.App.Endpoints;

public static class BucketListEndpoints
{
    private const long MaxMediaBytes = 50 * 1024 * 1024; // 50 MB pro Datei
    private static readonly string[] AllowedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private static readonly string[] AllowedVideoExtensions = [".mp4", ".mov", ".m4v", ".webm", ".avi"];

    public static RouteGroupBuilder MapBucketListEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/bucketlist");

        group.MapGet("/", GetEntriesAsync);
        group.MapPost("/", CreateEntryAsync);
        group.MapPost("/{id:guid}/complete", CompleteEntryAsync);
        group.MapPost("/{entryId:guid}/media/{mediaId:guid}/gallery", AddMediaToGalleryAsync);
        group.MapPost("/{entryId:guid}/media", UploadAdditionalMediaAsync);
        group.MapDelete("/{entryId:guid}/media/{mediaId:guid}", DeleteMediaAsync);
        group.MapPost("/verify-password", VerifyMasterPasswordAsync);

        return group;
    }

    private static async Task<Ok<List<BucketListEntryDto>>> GetEntriesAsync(LoveLetterDbContext db)
    {
        var entries = await db.BucketListEntries
            .Include(e => e.Media)
            .OrderBy(e => e.Completed)
            .ThenBy(e => e.CreatedAt)
            .Select(e => e.ToDto())
            .ToListAsync();

        return TypedResults.Ok(entries);
    }

    private static async Task<Results<BadRequest<string>, Ok<BucketListEntryDto>>> CreateEntryAsync(
        CreateBucketListEntryRequest request,
        LoveLetterDbContext db)
    {
        if (string.IsNullOrWhiteSpace(request.Title))
        {
            return TypedResults.BadRequest("Titel darf nicht leer sein.");
        }

        var entry = new BucketListEntry
        {
            Id = Guid.NewGuid(),
            Title = request.Title.Trim(),
            RequiresPhoto = request.RequiresPhoto,
            CreatedAt = DateTime.UtcNow
        };

        db.BucketListEntries.Add(entry);
        await db.SaveChangesAsync();

        return TypedResults.Ok(entry.ToDto());
    }

    private static async Task<IResult> CompleteEntryAsync(
        Guid id,
        HttpRequest request,
        LoveLetterDbContext db,
        IWebHostEnvironment env,
        IBucketThumbnailQueue thumbnailQueue)
    {
        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == id);
        if (entry is null)
        {
            return TypedResults.NotFound();
        }

        if (entry.Completed)
        {
            return TypedResults.BadRequest("Dieser Eintrag ist bereits abgeschlossen.");
        }

        var mediaFiles = new List<IFormFile>();
        if (request.HasFormContentType)
        {
            var form = await request.ReadFormAsync();
            mediaFiles.AddRange(form.Files.GetFiles("media"));

            if (!mediaFiles.Any())
            {
                var legacyPhoto = form.Files["photo"];
                if (legacyPhoto is not null)
                {
                    mediaFiles.Add(legacyPhoto);
                }
            }
        }

        if (entry.RequiresPhoto && !mediaFiles.Any())
        {
            return TypedResults.BadRequest("Bitte lade mindestens ein Foto oder Video hoch, um diesen Eintrag abzuschließen.");
        }

        var createdMedia = new List<BucketListMedia>();
        if (mediaFiles.Any())
        {
            var (savedMedia, errorResult) = await SaveUploadedMediaAsync(mediaFiles, entry.Id, env, thumbnailQueue);
            if (errorResult is not null)
            {
                return errorResult;
            }

            if (savedMedia is not null)
            {
                createdMedia.AddRange(savedMedia);
            }
        }

        if (createdMedia.Any())
        {
            db.BucketListMedia.AddRange(createdMedia);
        }

        entry.Completed = true;
        entry.CompletedAt = DateTime.UtcNow;

        await db.SaveChangesAsync();

        return TypedResults.Ok(entry.ToDto());
    }

    private static async Task<IResult> UploadAdditionalMediaAsync(
        Guid entryId,
        HttpRequest request,
        LoveLetterDbContext db,
        IWebHostEnvironment env,
        IOptions<BucketListSecurityOptions> securityOptions,
        IBucketThumbnailQueue thumbnailQueue)
    {
        if (!IsMasterPasswordValid(request, securityOptions.Value))
        {
            return TypedResults.BadRequest("Masterpasswort ist ungültig.");
        }

        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        if (entry is null)
        {
            return TypedResults.NotFound();
        }

        if (!entry.Completed)
        {
            return TypedResults.BadRequest("Bitte schließe den Eintrag zuerst ab, bevor du weitere Medien hinzufügst.");
        }

        if (!request.HasFormContentType)
        {
            return TypedResults.BadRequest("Ungültiges Formular.");
        }

        var form = await request.ReadFormAsync();
        var files = form.Files.GetFiles("media");
        if (files.Count == 0)
        {
            return TypedResults.BadRequest("Bitte wähle mindestens eine Datei aus.");
        }

        var (createdMedia, errorResult) = await SaveUploadedMediaAsync(files, entry.Id, env, thumbnailQueue);
        if (errorResult is not null)
        {
            return errorResult;
        }

        if (createdMedia?.Any() == true)
        {
            db.BucketListMedia.AddRange(createdMedia);
            await db.SaveChangesAsync();
        }

        return TypedResults.Ok(entry.ToDto());
    }

    private static async Task<IResult> DeleteMediaAsync(
        Guid entryId,
        Guid mediaId,
        HttpRequest request,
        LoveLetterDbContext db,
        IWebHostEnvironment env,
        IOptions<BucketListSecurityOptions> securityOptions)
    {
        if (!IsMasterPasswordValid(request, securityOptions.Value))
        {
            return TypedResults.BadRequest("Masterpasswort ist ungültig.");
        }

        var media = await db.BucketListMedia.FirstOrDefaultAsync(m => m.EntryId == entryId && m.Id == mediaId);
        if (media is null)
        {
            return TypedResults.NotFound();
        }

        db.BucketListMedia.Remove(media);
        await db.SaveChangesAsync();

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;
        var sanitized = media.FilePath.TrimStart('/', '\\');
        var filePath = Path.Combine(webRoot, sanitized);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == entryId);

        return entry is null
            ? TypedResults.Ok()
            : TypedResults.Ok(entry.ToDto());
    }

    private static async Task<IResult> AddMediaToGalleryAsync(
        Guid entryId,
        Guid mediaId,
        LoveLetterDbContext db,
        IWebHostEnvironment env)
    {
        var media = await db.BucketListMedia
            .Include(m => m.Entry)
            .FirstOrDefaultAsync(m => m.EntryId == entryId && m.Id == mediaId);

        if (media is null)
        {
            return TypedResults.NotFound();
        }

        if (media.IsVideo)
        {
            return TypedResults.BadRequest("Videos können nicht in die Galerie übernommen werden.");
        }

        if (media.IsInGallery)
        {
            return TypedResults.BadRequest("Dieses Medium befindet sich bereits in der Galerie.");
        }

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var sanitizedSource = media.FilePath.TrimStart('/', '\\');
        var sourcePath = Path.Combine(webRoot, sanitizedSource);
        if (!File.Exists(sourcePath))
        {
            return TypedResults.BadRequest("Die Originaldatei wurde nicht gefunden.");
        }

        var uploadsRoot = Path.Combine(webRoot, "uploads", "gallery");
        Directory.CreateDirectory(uploadsRoot);
        var extension = Path.GetExtension(media.FilePath);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var targetPath = Path.Combine(uploadsRoot, fileName);
        await using (var source = File.OpenRead(sourcePath))
        await using (var destination = File.Create(targetPath))
        {
            await source.CopyToAsync(destination);
        }

        var relativePath = Path.Combine("uploads", "gallery", fileName).Replace("\\", "/");
        var caption = media.Entry?.Title;

        var photo = new GalleryPhoto
        {
            Id = Guid.NewGuid(),
            Caption = caption,
            FilePath = relativePath,
            OriginalFileName = media.OriginalFileName,
            CreatedAt = DateTime.UtcNow
        };

        db.GalleryPhotos.Add(photo);
        media.IsInGallery = true;
        await db.SaveChangesAsync();

        return TypedResults.Ok(photo.ToDto());
    }

    private static bool IsMasterPasswordValid(HttpRequest request, BucketListSecurityOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.MasterPassword))
        {
            return true;
        }

        if (!request.Headers.TryGetValue("X-Master-Pass", out var provided) || string.IsNullOrWhiteSpace(provided))
        {
            return false;
        }

        return string.Equals(provided.ToString(), options.MasterPassword, StringComparison.Ordinal);
    }

    private static IResult VerifyMasterPasswordAsync(
        HttpRequest request,
        IOptions<BucketListSecurityOptions> options)
    {
        return IsMasterPasswordValid(request, options.Value)
            ? TypedResults.Ok()
            : TypedResults.BadRequest("Masterpasswort ist ungültig.");
    }

    private static async Task<(List<BucketListMedia>? Media, IResult? Error)> SaveUploadedMediaAsync(
        IEnumerable<IFormFile> mediaFiles,
        Guid entryId,
        IWebHostEnvironment env,
        IBucketThumbnailQueue thumbnailQueue)
    {
        var files = mediaFiles.ToList();
        if (!files.Any())
        {
            return ([], null);
        }

        var normalizedFiles = new List<(IFormFile File, bool IsVideo, string Extension)>();
        foreach (var file in files)
        {
            if (file.Length > MaxMediaBytes)
            {
                return (null, TypedResults.BadRequest("Eine Datei ist zu groß (maximal 50 MB)."));
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isVideo = AllowedVideoExtensions.Contains(extension);
            var isImage = AllowedImageExtensions.Contains(extension);

            if (!isVideo && !isImage)
            {
                return (null, TypedResults.BadRequest("Nur PNG, JPG, WEBP sowie MP4, MOV oder WEBM werden unterstützt."));
            }

            normalizedFiles.Add((file, isVideo, extension));
        }

        var webRoot = string.IsNullOrWhiteSpace(env.WebRootPath)
            ? Path.Combine(env.ContentRootPath, "wwwroot")
            : env.WebRootPath;

        var uploadsRoot = Path.Combine(webRoot, "uploads", "bucket", entryId.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var createdMedia = new List<BucketListMedia>();
        foreach (var (file, isVideo, extension) in normalizedFiles)
        {
            var fileName = $"{Guid.NewGuid()}{extension}";
            var fullPath = Path.Combine(uploadsRoot, fileName);
            await using (var stream = File.Create(fullPath))
            {
                await file.CopyToAsync(stream);
            }

            var relativePath = Path.Combine("uploads", "bucket", entryId.ToString(), fileName)
                .Replace("\\", "/");

            var mediaId = Guid.NewGuid();
            createdMedia.Add(new BucketListMedia
            {
                Id = mediaId,
                EntryId = entryId,
                FilePath = relativePath,
                OriginalFileName = file.FileName,
                ContentType = file.ContentType,
                IsVideo = isVideo,
                CreatedAt = DateTime.UtcNow
            });

            if (!isVideo)
            {
                await thumbnailQueue.EnqueueAsync(
                    new BucketThumbnailWorkItem(mediaId, fullPath, fileName));
            }
        }

        return (createdMedia, null);
    }
}
