using LoveLetter.App.Data;
using Microsoft.EntityFrameworkCore;

namespace LoveLetter.App.Services;

public sealed class ThumbnailBackfillService
{
    private readonly IDbContextFactory<LoveLetterDbContext> _dbContextFactory;
    private readonly IWebHostEnvironment _environment;
    private readonly IImageThumbnailService _thumbnailService;
    private readonly ILogger<ThumbnailBackfillService> _logger;

    public ThumbnailBackfillService(
        IDbContextFactory<LoveLetterDbContext> dbContextFactory,
        IWebHostEnvironment environment,
        IImageThumbnailService thumbnailService,
        ILogger<ThumbnailBackfillService> logger)
    {
        _dbContextFactory = dbContextFactory;
        _environment = environment;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    public async Task RunAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await BackfillGalleryPhotosAsync(cancellationToken);
            await BackfillBucketMediaAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail backfill failed");
        }
    }

    private async Task BackfillGalleryPhotosAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var photos = await db.GalleryPhotos
            .Where(p => p.ThumbnailPath == null && !string.IsNullOrWhiteSpace(p.FilePath))
            .ToListAsync(cancellationToken);

        if (photos.Count == 0)
        {
            return;
        }

        var webRoot = GetWebRoot();
        foreach (var photo in photos)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = Path.Combine(webRoot, photo.FilePath.TrimStart('/', '\\'));
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var baseDirectory = Path.GetDirectoryName(sourcePath) ?? webRoot;
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                sourcePath,
                Path.Combine(baseDirectory, "thumbs"),
                fileName,
                512,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(thumbnailPath))
            {
                photo.ThumbnailPath = GalleryService.NormalizeRelativePath(webRoot, thumbnailPath);
            }
            else
            {
                photo.ThumbnailPath = photo.FilePath;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task BackfillBucketMediaAsync(CancellationToken cancellationToken)
    {
        await using var db = await _dbContextFactory.CreateDbContextAsync(cancellationToken);
        var mediaItems = await db.BucketListMedia
            .Where(m => !m.IsVideo && m.ThumbnailPath == null && !string.IsNullOrWhiteSpace(m.FilePath))
            .ToListAsync(cancellationToken);

        if (mediaItems.Count == 0)
        {
            return;
        }

        var webRoot = GetWebRoot();
        foreach (var media in mediaItems)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var sourcePath = Path.Combine(webRoot, media.FilePath.TrimStart('/', '\\'));
            if (!File.Exists(sourcePath))
            {
                continue;
            }

            var baseDirectory = Path.GetDirectoryName(sourcePath) ?? webRoot;
            var fileName = Path.GetFileNameWithoutExtension(sourcePath);
            var thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                sourcePath,
                Path.Combine(baseDirectory, "thumbs"),
                fileName,
                512,
                cancellationToken);

            if (!string.IsNullOrWhiteSpace(thumbnailPath))
            {
                media.ThumbnailPath = BucketListService.NormalizeRelativePath(webRoot, thumbnailPath);
            }
            else
            {
                media.ThumbnailPath = media.FilePath;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
    }

    private string GetWebRoot()
    {
        return string.IsNullOrWhiteSpace(_environment.WebRootPath)
            ? Path.Combine(_environment.ContentRootPath, "wwwroot")
            : _environment.WebRootPath;
    }
}
