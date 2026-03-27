using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.FileSystem.Diagnostics;
using Granit.BlobStorage.FileSystem.Options;
using Granit.BlobStorage.Internal;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.FileSystem.Internal;

/// <summary>
/// Local file system implementation of <see cref="IBlobStoreProvider"/>.
/// Stores blobs as files on disk in a tenant-prefixed directory structure.
/// </summary>
/// <remarks>
/// This provider does NOT implement <see cref="IPresignedUrlProvider"/>.
/// Pre-signed URLs are provided by <c>Granit.BlobStorage.Proxy</c>.
/// </remarks>
// Infrastructure adapter over System.IO. Integration tests use temp directories.
[ExcludeFromCodeCoverage]
internal sealed class FileSystemBlobClient(IOptions<FileSystemBlobOptions> options) : IBlobStoreProvider
{
    private string BasePath => options.Value.BasePath;

    /// <inheritdoc/>
    public async Task SaveAsync(
        string bucket,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageFileSystemActivitySource.Source.StartActivity(BlobStorageFileSystemActivitySource.Save);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagBasePath, bucket);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagContentType, contentType);

        string filePath = ResolvePath(objectKey);
        string? directory = Path.GetDirectoryName(filePath);
        if (directory is not null)
        {
            Directory.CreateDirectory(directory);
        }

        FileStream fileStream = new(filePath, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 81920, useAsync: true);
        await using (fileStream.ConfigureAwait(false))
        {
            await content.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageFileSystemActivitySource.Source.StartActivity(BlobStorageFileSystemActivitySource.Read);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagBasePath, bucket);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagObjectKey, objectKey);

        string filePath = ResolvePath(objectKey);
        Stream stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 81920, useAsync: true);
        return Task.FromResult(stream);
    }

    /// <inheritdoc/>
    public Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageFileSystemActivitySource.Source.StartActivity(BlobStorageFileSystemActivitySource.Delete);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagBasePath, bucket);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagObjectKey, objectKey);

        string filePath = ResolvePath(objectKey);
        if (File.Exists(filePath))
        {
            File.Delete(filePath);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public Task<long> GetSizeAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageFileSystemActivitySource.Source.StartActivity(BlobStorageFileSystemActivitySource.GetSize);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagBasePath, bucket);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagObjectKey, objectKey);

        string filePath = ResolvePath(objectKey);
        FileInfo fileInfo = new(filePath);
        return Task.FromResult(fileInfo.Length);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenPartialReadAsync(
        string bucket,
        string objectKey,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageFileSystemActivitySource.Source.StartActivity(BlobStorageFileSystemActivitySource.PartialStream);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagBasePath, bucket);
        activity?.SetTag(BlobStorageFileSystemActivitySource.TagObjectKey, objectKey);

        string filePath = ResolvePath(objectKey);

        FileStream fileStream = new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: byteCount, useAsync: true);
        await using (fileStream.ConfigureAwait(false))
        {
            byte[] buffer = new byte[byteCount];
            int bytesRead = await fileStream.ReadAsync(buffer.AsMemory(0, byteCount), cancellationToken).ConfigureAwait(false);
            return new MemoryStream(buffer, 0, bytesRead, writable: false);
        }
    }

    /// <summary>
    /// Resolves the full file path for a blob.
    /// <see cref="BasePath"/> is used as the root directory.
    /// The <paramref name="objectKey"/> maps to the relative path within <see cref="BasePath"/>.
    /// </summary>
    private string ResolvePath(string objectKey)
    {
        string fullPath = Path.GetFullPath(Path.Join(BasePath, objectKey));
        string resolvedBase = Path.GetFullPath(BasePath);

        if (!fullPath.StartsWith(resolvedBase, StringComparison.Ordinal))
        {
            throw new ArgumentException("Object key must not escape the base storage directory.", nameof(objectKey));
        }

        return fullPath;
    }
}
