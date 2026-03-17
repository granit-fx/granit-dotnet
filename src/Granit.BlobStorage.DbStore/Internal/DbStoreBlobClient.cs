using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Granit.BlobStorage.DbStore.Diagnostics;
using Granit.BlobStorage.DbStore.Entities;
using Granit.BlobStorage.DbStore.Internal;
using Granit.BlobStorage.DbStore.Options;
using Granit.BlobStorage.Internal;
using Granit.Guids;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.DbStore.Internal;

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
internal sealed class DbStoreBlobClient(
    IDbContextFactory<DbStoreBlobStorageDbContext> contextFactory,
    IOptions<DbStoreBlobOptions> options,
    IClock clock,
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
        using Activity? activity = BlobStorageDbStoreActivitySource.Source.StartActivity(BlobStorageDbStoreActivitySource.Save);
        activity?.SetTag(BlobStorageDbStoreActivitySource.TagObjectKey, objectKey);
        activity?.SetTag(BlobStorageDbStoreActivitySource.TagContentType, contentType);

        using MemoryStream ms = new();
        await content.CopyToAsync(ms, cancellationToken).ConfigureAwait(false);
        byte[] bytes = ms.ToArray();

        if (bytes.Length > options.Value.MaxBlobSizeBytes)
        {
            throw new InvalidOperationException(
                $"Blob size ({bytes.Length} bytes) exceeds the maximum allowed size ({options.Value.MaxBlobSizeBytes} bytes).");
        }

        await using DbStoreBlobStorageDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DbStoreBlobContent entity = new()
        {
            Id = guidGenerator.Create(),
            ObjectKey = objectKey,
            Content = bytes,
            CreatedAt = clock.Now,
        };

        context.BlobContents.Add(entity);
        await context.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<Stream> OpenReadAsync(
        string bucket,
        string objectKey,
        CancellationToken cancellationToken = default)
    {
        using Activity? activity = BlobStorageDbStoreActivitySource.Source.StartActivity(BlobStorageDbStoreActivitySource.Read);
        activity?.SetTag(BlobStorageDbStoreActivitySource.TagObjectKey, objectKey);

        await using DbStoreBlobStorageDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DbStoreBlobContent entity = await context.BlobContents
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
        using Activity? activity = BlobStorageDbStoreActivitySource.Source.StartActivity(BlobStorageDbStoreActivitySource.Delete);
        activity?.SetTag(BlobStorageDbStoreActivitySource.TagObjectKey, objectKey);

        await using DbStoreBlobStorageDbContext context =
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
        using Activity? activity = BlobStorageDbStoreActivitySource.Source.StartActivity(BlobStorageDbStoreActivitySource.GetSize);
        activity?.SetTag(BlobStorageDbStoreActivitySource.TagObjectKey, objectKey);

        await using DbStoreBlobStorageDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DbStoreBlobContent entity = await context.BlobContents
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
        using Activity? activity = BlobStorageDbStoreActivitySource.Source.StartActivity(BlobStorageDbStoreActivitySource.PartialStream);
        activity?.SetTag(BlobStorageDbStoreActivitySource.TagObjectKey, objectKey);

        await using DbStoreBlobStorageDbContext context =
            await contextFactory.CreateDbContextAsync(cancellationToken).ConfigureAwait(false);

        DbStoreBlobContent entity = await context.BlobContents
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.ObjectKey == objectKey, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new FileNotFoundException($"Blob not found: {objectKey}");

        int length = Math.Min(byteCount, entity.Content.Length);
        return new MemoryStream(entity.Content, 0, length, writable: false);
    }
}
