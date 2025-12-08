using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace LoveLetter.App.Services;

public sealed class UploadedMediaFile
{
    private readonly Func<CancellationToken, Task<Stream>> _openReadStream;

    public UploadedMediaFile(string fileName, string contentType, long length, Func<CancellationToken, Task<Stream>> openReadStream)
    {
        FileName = fileName;
        ContentType = contentType;
        Length = length;
        _openReadStream = openReadStream;
    }

    public string FileName { get; }
    public string ContentType { get; }
    public long Length { get; }

    public Task<Stream> OpenReadStreamAsync(CancellationToken cancellationToken = default)
    {
        return _openReadStream(cancellationToken);
    }
}
