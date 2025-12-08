using System.IO;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Webp;
using SixLabors.ImageSharp.Processing;

namespace LoveLetter.App.Services;

public interface IImageThumbnailService
{
    Task<string?> GenerateThumbnailAsync(
        string sourcePath,
        string destinationDirectory,
        string fileNameBase,
        int maxSize,
        CancellationToken cancellationToken = default);
}

public sealed class ImageThumbnailService : IImageThumbnailService
{
    public async Task<string?> GenerateThumbnailAsync(
        string sourcePath,
        string destinationDirectory,
        string fileNameBase,
        int maxSize,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sourcePath) || !File.Exists(sourcePath))
        {
            return null;
        }

        Directory.CreateDirectory(destinationDirectory);
        var targetPath = Path.Combine(destinationDirectory, $"{fileNameBase}-thumb.webp");

        try
        {
            using var image = await Image.LoadAsync(sourcePath, cancellationToken);
            var longestEdge = Math.Max(image.Width, image.Height);
            if (longestEdge > maxSize)
            {
                var ratio = maxSize / (double)longestEdge;
                var width = Math.Max(1, (int)Math.Round(image.Width * ratio));
                var height = Math.Max(1, (int)Math.Round(image.Height * ratio));
                image.Mutate(ctx => ctx.Resize(width, height));
            }

            var encoder = new WebpEncoder { Quality = 75 };
            await image.SaveAsync(targetPath, encoder, cancellationToken);
            return targetPath;
        }
        catch
        {
            return null;
        }
    }
}
