using System.IO;
using LoveLetter.App.Configuration;
using LoveLetter.App.Data;
using LoveLetter.App.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using BucketListMediaEntity = LoveLetter.App.Data.BucketListMedia;

namespace LoveLetter.App.Services;

public interface IBucketListService
{
    Task<IReadOnlyList<BucketListEntryDto>> GetEntriesAsync(CancellationToken cancellationToken = default);
    Task<ServiceResult<BucketListEntryDto>> CreateEntryAsync(string title, bool requiresPhoto, CancellationToken cancellationToken = default);
    Task<ServiceResult<BucketListEntryDto>> CompleteEntryAsync(Guid entryId, IReadOnlyList<UploadedMediaFile>? mediaFiles, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<BucketListEntryDto>> UploadAdditionalMediaAsync(Guid entryId, IReadOnlyList<UploadedMediaFile> mediaFiles, string? masterPassword, IProgress<int>? progress = null, CancellationToken cancellationToken = default);
    Task<ServiceResult<BucketListEntryDto>> RemoveMediaAsync(Guid entryId, Guid mediaId, string? masterPassword, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> VerifyMasterPasswordAsync(string? masterPassword);
    Task<ServiceResult<bool>> AddMediaToGalleryAsync(Guid entryId, Guid mediaId, CancellationToken cancellationToken = default);
    Task<ServiceResult<bool>> DeleteEntryAsync(Guid entryId, string? masterPassword, CancellationToken cancellationToken = default);
}

public sealed class BucketListService : IBucketListService
{
    private const long MaxMediaBytes = 50 * 1024 * 1024;
    private static readonly string[] AllowedImageExtensions = [".png", ".jpg", ".jpeg", ".webp"];
    private static readonly string[] AllowedVideoExtensions = [".mp4", ".mov", ".m4v", ".webm", ".avi"];

    private readonly IDbContextFactory<LoveLetterDbContext> _dbContextFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly BucketListSecurityOptions _securityOptions;
    private readonly IImageThumbnailService _thumbnailService;
    private readonly IBucketThumbnailQueue _thumbnailQueue;

    public BucketListService(
        IDbContextFactory<LoveLetterDbContext> dbContextFactory,
        IWebHostEnvironment environment,
        IOptions<BucketListSecurityOptions> securityOptions,
        IImageThumbnailService thumbnailService,
        IBucketThumbnailQueue thumbnailQueue)
    {
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _securityOptions = securityOptions.Value;
        _thumbnailService = thumbnailService;
        _thumbnailQueue = thumbnailQueue;
    }

    public async Task<IReadOnlyList<BucketListEntryDto>> GetEntriesAsync(CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entries = await db.BucketListEntries
            .Include(e => e.Media)
            .OrderBy(e => e.Completed)
            .ThenBy(e => e.CreatedAt)
            .ToListAsync(cancellationToken);

        return entries.Select(e => e.ToDto()).ToList();
    }

    public async Task<ServiceResult<BucketListEntryDto>> CreateEntryAsync(string title, bool requiresPhoto, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return ServiceResult<BucketListEntryDto>.Fail("Titel darf nicht leer sein.");
        }

        if (title.Length > 160)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Titel ist zu lang (max. 160 Zeichen).");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = new BucketListEntry
        {
            Id = Guid.NewGuid(),
            Title = title.Trim(),
            RequiresPhoto = requiresPhoto,
            CreatedAt = DateTime.UtcNow
        };

        db.BucketListEntries.Add(entry);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<BucketListEntryDto>.Ok(entry.ToDto());
    }

    public async Task<ServiceResult<BucketListEntryDto>> CompleteEntryAsync(Guid entryId, IReadOnlyList<UploadedMediaFile>? mediaFiles, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Eintrag wurde nicht gefunden.");
        }

        if (entry.Completed)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Dieser Eintrag ist bereits abgeschlossen.");
        }

        var pendingFiles = mediaFiles?.ToList() ?? [];
        if (entry.RequiresPhoto && pendingFiles.Count == 0)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Bitte lade mindestens ein Foto oder Video hoch, um diesen Eintrag abzuschließen.");
        }

        if (pendingFiles.Count > 0)
        {
            var savedMediaResult = await SaveUploadedMediaAsync(entry.Id, pendingFiles, progress, cancellationToken);
            if (!savedMediaResult.Success)
            {
                return ServiceResult<BucketListEntryDto>.Fail(savedMediaResult.Error ?? "Upload fehlgeschlagen.");
            }

            if (savedMediaResult.Value is { Count: > 0 })
            {
                db.BucketListMedia.AddRange(savedMediaResult.Value);
            }
        }

        entry.Completed = true;
        entry.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<BucketListEntryDto>.Ok(entry.ToDto());
    }

    public async Task<ServiceResult<BucketListEntryDto>> UploadAdditionalMediaAsync(Guid entryId, IReadOnlyList<UploadedMediaFile> mediaFiles, string? masterPassword, IProgress<int>? progress = null, CancellationToken cancellationToken = default)
    {
        if (mediaFiles.Count == 0)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Bitte wähle mindestens eine Datei aus.");
        }

        var passwordCheck = ValidateMasterPassword(masterPassword);
        if (!passwordCheck.Success)
        {
            return ServiceResult<BucketListEntryDto>.Fail(passwordCheck.Error ?? "Masterpasswort ist ungültig.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Eintrag wurde nicht gefunden.");
        }

        if (!entry.Completed)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Bitte schließe den Eintrag zuerst ab, bevor du weitere Medien hinzufügst.");
        }

        var uploadResult = await SaveUploadedMediaAsync(entry.Id, mediaFiles.ToList(), progress, cancellationToken);
        if (!uploadResult.Success)
        {
            return ServiceResult<BucketListEntryDto>.Fail(uploadResult.Error ?? "Upload fehlgeschlagen.");
        }

        if (uploadResult.Value is { Count: > 0 })
        {
            db.BucketListMedia.AddRange(uploadResult.Value);
            await db.SaveChangesAsync(cancellationToken);
        }

        return ServiceResult<BucketListEntryDto>.Ok(entry.ToDto());
    }

    public async Task<ServiceResult<BucketListEntryDto>> RemoveMediaAsync(Guid entryId, Guid mediaId, string? masterPassword, CancellationToken cancellationToken = default)
    {
        var passwordCheck = ValidateMasterPassword(masterPassword);
        if (!passwordCheck.Success)
        {
            return ServiceResult<BucketListEntryDto>.Fail(passwordCheck.Error ?? "Masterpasswort ist ungültig.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.BucketListMedia
            .Include(m => m.Entry)
            .FirstOrDefaultAsync(m => m.EntryId == entryId && m.Id == mediaId, cancellationToken);

        if (media is null)
        {
            return ServiceResult<BucketListEntryDto>.Fail("Medium nicht gefunden.");
        }

        db.BucketListMedia.Remove(media);
        await db.SaveChangesAsync(cancellationToken);

        TryDeleteFile(media.FilePath);
        TryDeleteFile(media.ThumbnailPath);

        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstAsync(e => e.Id == entryId, cancellationToken);

        return ServiceResult<BucketListEntryDto>.Ok(entry.ToDto());
    }

    public Task<ServiceResult<bool>> VerifyMasterPasswordAsync(string? masterPassword)
    {
        return Task.FromResult(ValidateMasterPassword(masterPassword));
    }

    public async Task<ServiceResult<bool>> AddMediaToGalleryAsync(Guid entryId, Guid mediaId, CancellationToken cancellationToken = default)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.BucketListMedia
            .Include(m => m.Entry)
            .FirstOrDefaultAsync(m => m.EntryId == entryId && m.Id == mediaId, cancellationToken);

        if (media is null)
        {
            return ServiceResult<bool>.Fail("Medium nicht gefunden.");
        }

        if (media.IsVideo)
        {
            return ServiceResult<bool>.Fail("Videos können nicht in die Galerie übernommen werden.");
        }

        if (media.IsInGallery)
        {
            return ServiceResult<bool>.Fail("Dieses Medium befindet sich bereits in der Galerie.");
        }

        var webRoot = GetWebRoot();
        var sanitizedSource = media.FilePath.TrimStart('/', '\\');
        var sourcePath = Path.Combine(webRoot, sanitizedSource);
        if (!File.Exists(sourcePath))
        {
            return ServiceResult<bool>.Fail("Die Originaldatei wurde nicht gefunden.");
        }

        var galleryRoot = Path.Combine(webRoot, "uploads", "gallery");
        Directory.CreateDirectory(galleryRoot);
        var extension = Path.GetExtension(media.FilePath);
        var fileName = $"{Guid.NewGuid()}{extension}";
        var targetPath = Path.Combine(galleryRoot, fileName);

        await using (var source = File.OpenRead(sourcePath))
        await using (var destination = File.Create(targetPath))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        var relativePath = Path.Combine("uploads", "gallery", fileName).Replace("\\", "/");
        string? thumbnailRelativePath = null;
        var thumbnailDirectory = Path.Combine(galleryRoot, "thumbs");
        var thumbnailFullPath = await _thumbnailService.GenerateThumbnailAsync(
            targetPath,
            thumbnailDirectory,
            Path.GetFileNameWithoutExtension(fileName),
            512,
            cancellationToken);
        if (!string.IsNullOrWhiteSpace(thumbnailFullPath))
        {
            thumbnailRelativePath = NormalizeRelativePath(webRoot, thumbnailFullPath!);
        }
        else
        {
            thumbnailRelativePath = relativePath;
        }
        var photo = new GalleryPhoto
        {
            Id = Guid.NewGuid(),
            Caption = media.Entry?.Title,
            FilePath = relativePath,
            ThumbnailPath = thumbnailRelativePath,
            OriginalFileName = media.OriginalFileName,
            CreatedAt = DateTime.UtcNow
        };

        db.GalleryPhotos.Add(photo);
        media.IsInGallery = true;
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    public async Task<ServiceResult<bool>> DeleteEntryAsync(Guid entryId, string? masterPassword, CancellationToken cancellationToken = default)
    {
        var passwordCheck = ValidateMasterPassword(masterPassword);
        if (!passwordCheck.Success)
        {
            return ServiceResult<bool>.Fail(passwordCheck.Error ?? "Masterpasswort ist ungültig.");
        }

        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var entry = await db.BucketListEntries
            .Include(e => e.Media)
            .FirstOrDefaultAsync(e => e.Id == entryId, cancellationToken);

        if (entry is null)
        {
            return ServiceResult<bool>.Fail("Eintrag wurde nicht gefunden.");
        }

        foreach (var media in entry.Media)
        {
            TryDeleteFile(media.FilePath);
            TryDeleteFile(media.ThumbnailPath);
        }

        db.BucketListEntries.Remove(entry);
        await db.SaveChangesAsync(cancellationToken);

        return ServiceResult<bool>.Ok(true);
    }

    private async Task<ServiceResult<List<BucketListMediaEntity>>> SaveUploadedMediaAsync(Guid entryId, IReadOnlyList<UploadedMediaFile> files, IProgress<int>? progress, CancellationToken cancellationToken)
    {
        if (files.Count == 0)
        {
            return ServiceResult<List<BucketListMediaEntity>>.Ok([]);
        }

        var normalized = new List<(UploadedMediaFile File, bool IsVideo, string Extension)>();
        foreach (var file in files)
        {
            if (file.Length == 0)
            {
                return ServiceResult<List<BucketListMediaEntity>>.Fail("Bitte wähle keine leeren Dateien aus.");
            }

            if (file.Length > MaxMediaBytes)
            {
                return ServiceResult<List<BucketListMediaEntity>>.Fail("Eine Datei ist zu groß (maximal 50 MB).");
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var isVideo = AllowedVideoExtensions.Contains(extension);
            var isImage = AllowedImageExtensions.Contains(extension);

            if (!isVideo && !isImage)
            {
                return ServiceResult<List<BucketListMediaEntity>>.Fail("Nur PNG, JPG, WEBP sowie MP4, MOV oder WEBM werden unterstützt.");
            }

            normalized.Add((file, isVideo, extension));
        }

        var webRoot = GetWebRoot();
        var uploadsRoot = Path.Combine(webRoot, "uploads", "bucket", entryId.ToString());
        Directory.CreateDirectory(uploadsRoot);

        var created = new List<BucketListMediaEntity>();
        foreach (var (file, isVideo, extension) in normalized)
        {
            var fileName = $"{Guid.NewGuid()}{extension}";
            var destinationPath = Path.Combine(uploadsRoot, fileName);
            await using (var destinationStream = File.Create(destinationPath))
            await using (var sourceStream = await file.OpenReadStreamAsync(cancellationToken))
            {
                await sourceStream.CopyToAsync(destinationStream, cancellationToken);
            }

            var relativePath = Path.Combine("uploads", "bucket", entryId.ToString(), fileName)
                .Replace("\\", "/");

            var mediaId = Guid.NewGuid();
            created.Add(new BucketListMediaEntity
            {
                Id = mediaId,
                EntryId = entryId,
                FilePath = relativePath,
                ThumbnailPath = null,
                OriginalFileName = file.FileName,
                ContentType = string.IsNullOrWhiteSpace(file.ContentType) ? null : file.ContentType,
                IsVideo = isVideo,
                CreatedAt = DateTime.UtcNow
            });

            if (!isVideo)
            {
                await _thumbnailQueue.EnqueueAsync(
                    new BucketThumbnailWorkItem(mediaId, destinationPath, fileName),
                    cancellationToken);
            }

            progress?.Report(created.Count);
        }

        return ServiceResult<List<BucketListMediaEntity>>.Ok(created);
    }

    private ServiceResult<bool> ValidateMasterPassword(string? provided)
    {
        if (string.IsNullOrWhiteSpace(_securityOptions.MasterPassword))
        {
            return ServiceResult<bool>.Ok(true);
        }

        if (string.IsNullOrWhiteSpace(provided))
        {
            return ServiceResult<bool>.Fail("Masterpasswort ist ungültig.");
        }

        return string.Equals(_securityOptions.MasterPassword, provided, StringComparison.Ordinal)
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Fail("Masterpasswort ist ungültig.");
    }

    private string GetWebRoot()
    {
        return string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
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

    internal async Task<string?> GenerateThumbnailRelativePathAsync(string sourcePath, string baseDirectory, string fileName, CancellationToken cancellationToken)
    {
        var thumbnailDirectory = Path.Combine(baseDirectory, "thumbs");
        var thumbnailFullPath = await _thumbnailService.GenerateThumbnailAsync(
            sourcePath,
            thumbnailDirectory,
            Path.GetFileNameWithoutExtension(fileName),
            512,
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
}
