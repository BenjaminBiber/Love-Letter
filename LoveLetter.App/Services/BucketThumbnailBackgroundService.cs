using System.IO;
using System.Threading.Channels;
using LoveLetter.App.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace LoveLetter.App.Services;

public interface IBucketThumbnailQueue
{
    ValueTask EnqueueAsync(BucketThumbnailWorkItem item, CancellationToken cancellationToken = default);
    IAsyncEnumerable<BucketThumbnailWorkItem> DequeueAsync(CancellationToken cancellationToken);
}

public sealed record BucketThumbnailWorkItem(Guid MediaId, string AbsolutePath, string FileName);

public sealed class BucketThumbnailQueue : IBucketThumbnailQueue
{
    private readonly Channel<BucketThumbnailWorkItem> _channel;

    public BucketThumbnailQueue()
    {
        _channel = Channel.CreateUnbounded<BucketThumbnailWorkItem>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = false
        });
    }

    public ValueTask EnqueueAsync(BucketThumbnailWorkItem item, CancellationToken cancellationToken = default)
    {
        return _channel.Writer.WriteAsync(item, cancellationToken);
    }

    public IAsyncEnumerable<BucketThumbnailWorkItem> DequeueAsync(CancellationToken cancellationToken)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }
}

public sealed class BucketThumbnailBackgroundService(
    IBucketThumbnailQueue queue,
    IDbContextFactory<LoveLetterDbContext> dbContextFactory,
    IWebHostEnvironment environment,
    IImageThumbnailService thumbnailService,
    ILogger<BucketThumbnailBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var item in queue.DequeueAsync(stoppingToken))
        {
            try
            {
                await ProcessItemAsync(item, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // graceful shutdown
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to generate thumbnail for bucket media {MediaId}", item.MediaId);
            }
        }
    }

    private async Task ProcessItemAsync(BucketThumbnailWorkItem item, CancellationToken cancellationToken)
    {
        if (!File.Exists(item.AbsolutePath))
        {
            return;
        }

        var fileDirectory = Path.GetDirectoryName(item.AbsolutePath);
        if (string.IsNullOrWhiteSpace(fileDirectory))
        {
            return;
        }

        var thumbnailDirectory = Path.Combine(fileDirectory, "thumbs");
        var baseFileName = Path.GetFileNameWithoutExtension(item.FileName);
        var thumbnailPath = await thumbnailService.GenerateThumbnailAsync(
            item.AbsolutePath,
            thumbnailDirectory,
            baseFileName,
            512,
            cancellationToken);

        if (string.IsNullOrWhiteSpace(thumbnailPath))
        {
            return;
        }

        await using var db = await dbContextFactory.CreateDbContextAsync(cancellationToken);
        var media = await db.BucketListMedia.FirstOrDefaultAsync(m => m.Id == item.MediaId, cancellationToken);
        if (media is null)
        {
            return;
        }

        var webRoot = string.IsNullOrWhiteSpace(environment.WebRootPath)
            ? Path.Combine(environment.ContentRootPath, "wwwroot")
            : environment.WebRootPath;

        media.ThumbnailPath = BucketListService.NormalizeRelativePath(webRoot, thumbnailPath);
        await db.SaveChangesAsync(cancellationToken);
    }
}
