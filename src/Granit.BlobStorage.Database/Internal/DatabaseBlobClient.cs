using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.Database.Diagnostics;
using Granit.BlobStorage.Database.Domain;
using Granit.BlobStorage.Database.Options;
using Granit.BlobStorage.Internal;
using Granit.Guids;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Database.Internal;

/// <summary>
/// Database implementation of <see cref="IBlobStoreProvider"/>.
/// Stores blob content as rows in a relational database via EF Core.
/// </summary>
/// <remarks>
/// This provider does NOT implement <see cref="IPresignedUrlProvider"/>.
/// Pre-signed URLs are provided by <c>Granit.BlobStorage.Proxy</c>.
/// </remarks>
// Infrastructure adapter over EF Core. Integration tests use in-memory database.
[ExcludeFromCodeCoverage]
internal sealed class DatabaseBlobClient(
    IDbContextFactory<BlobStorageDatabaseDbContext> contextFactory,
    IOptions<DatabaseBlobOptions> options,
    IGuidGenerator guidGenerator) : IBlobStoreProvider
{
    /// <inheritdoc/>
    public async Task SaveAsync(
        string bucket,
        string objectKey,
        Stream content,
        string contentType,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageDatabaseActivitySource.Source.StartActivity(BlobStorageDatabaseActivitySource.Save);
        activity?.SetTag(BlobStorageDatabaseActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageDatabaseActivitySource.TagContentType, contentType);

        long maxBytes = options.Value.MaxBlobSizeBytes;

        // Pre-flight size check when the stream is seekable; cheaper than buffering an oversized payload.
        if (content.CanSeek && content.Length > maxBytes)
        {
            throw new InvalidOperationException(
                $"Blob size ({content.Length} bytes) exceeds the maximum allowed size ({maxBytes} bytes).");
        }

        // EF Core maps the column as `byte[]`, which requires full materialization before SaveChanges;
        // we still cap the read at MaxBlobSizeBytes + 1 to fail fast on oversize unseekable streams.
        await using MemoryStream ms = new();
        byte[] buffer = new byte[81920];
        long totalRead = 0;
        int read;
        while ((read = await content.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false)) > 0)
        {
            totalRead += read;
            if (totalRead > maxBytes)
            {
                throw new InvalidOperationException(
                    $"Blob size exceeds the maximum allowed size ({maxBytes} bytes).");
            }

            await ms.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        await using BlobStorageDatabaseDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        var entity = DatabaseBlobContent.Create(
            guidGenerator.Create(),
            objectKey,
            ms.ToArray());

        context.BlobContents.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageDatabaseActivitySource.Source.StartActivity(BlobStorageDatabaseActivitySource.Read);
        activity?.SetTag(BlobStorageDatabaseActivitySource.TagObjectKey, objectKey);

        await using BlobStorageDatabaseDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DatabaseBlobContent entity = await context.BlobContents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ObjectKey == objectKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new FileNotFoundException($"Blob not found: {objectKey}");

        return new MemoryStream(entity.Content, writable: false);
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageDatabaseActivitySource.Source.StartActivity(BlobStorageDatabaseActivitySource.Delete);
        activity?.SetTag(BlobStorageDatabaseActivitySource.TagObjectKey, objectKey);

        await using BlobStorageDatabaseDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        await context.BlobContents
            .Where(e => e.ObjectKey == objectKey)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<long> GetSizeAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageDatabaseActivitySource.Source.StartActivity(BlobStorageDatabaseActivitySource.GetSize);
        activity?.SetTag(BlobStorageDatabaseActivitySource.TagObjectKey, objectKey);

        await using BlobStorageDatabaseDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DatabaseBlobContent entity = await context.BlobContents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ObjectKey == objectKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new FileNotFoundException($"Blob not found: {objectKey}");

        return entity.Content.Length;
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenPartialReadAsync(
        string bucket,
        string objectKey,
        int byteCount,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageDatabaseActivitySource.Source.StartActivity(BlobStorageDatabaseActivitySource.PartialStream);
        activity?.SetTag(BlobStorageDatabaseActivitySource.TagObjectKey, objectKey);

        await using BlobStorageDatabaseDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DatabaseBlobContent entity = await context.BlobContents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ObjectKey == objectKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new FileNotFoundException($"Blob not found: {objectKey}");

        int length = Math.Min(byteCount, entity.Content.Length);
        return new MemoryStream(entity.Content, 0, length, writable: false);
    }
}
