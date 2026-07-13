using System.Runtime.CompilerServices;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using Granit.Privacy.DataExport.Sanitization;
using Granit.Privacy.DataExport.Security;
using Granit.Privacy.Options;
using Microsoft.Extensions.Options;

namespace Granit.Privacy.BlobStorage.Streaming;

/// <summary>
/// Default <see cref="IBlobBackedExportSource"/> — sanitises paths, signs HMAC
/// capabilities, and yields fragments without touching the source bytes.
/// </summary>
public sealed class BlobBackedExportSource(
    IExportHmacSigner hmacSigner,
    IOptions<GranitPrivacyOptions> options,
    TimeProvider timeProvider) : IBlobBackedExportSource
{
    /// <inheritdoc />
    public IAsyncEnumerable<ExportFragment> StreamAsync(
        IAsyncEnumerable<BlobBackedExportItem> items,
        PrivacyExportContext context,
        string providerName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrEmpty(providerName);
        return StreamCoreAsync(items, context, providerName, cancellationToken);
    }

    private async IAsyncEnumerable<ExportFragment> StreamCoreAsync(
        IAsyncEnumerable<BlobBackedExportItem> items,
        PrivacyExportContext context,
        string providerName,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Capability TTL: saga timeout + worker headroom. Same envelope StagedFragmentBuilder
        // uses, so a single rotation horizon covers both fragment kinds.
        DateTimeOffset expiresAt = timeProvider.GetUtcNow()
            + TimeSpan.FromMinutes(options.Value.ExportTimeoutMinutes * 4);

        await foreach (BlobBackedExportItem item in items.WithCancellation(cancellationToken).ConfigureAwait(false))
        {
            ArgumentNullException.ThrowIfNull(item);
            ArgumentException.ThrowIfNullOrEmpty(item.SourceContainer);
            ArgumentException.ThrowIfNullOrEmpty(item.ContentType);
            ArgumentNullException.ThrowIfNull(item.SourceBlob);

            string safeEntryPath = EntryPathSanitizer.Sanitize(item.EntryPath);
            Guid sourceBlobId = ParseBlobId(item.SourceBlob.Value);

            string integrityTag = await hmacSigner.SignAsync(new ExportHmacParameters(
                RequestId: context.RequestId,
                SubjectUserId: context.SubjectUserId,
                ProviderName: providerName,
                FragmentKind: "passthrough",
                SourceContainer: item.SourceContainer,
                SourceBlobId: sourceBlobId,
                EntryPath: safeEntryPath,
                ExpiresAt: expiresAt), cancellationToken).ConfigureAwait(false);

            yield return new PassThroughExportFragment
            {
                EntryPath = safeEntryPath,
                ContentType = item.ContentType,
                KnownSizeBytes = item.KnownSizeBytes,
                IntegrityTag = integrityTag,
                SourceContainer = item.SourceContainer,
                SourceBlob = item.SourceBlob,
            };
        }
    }

    private static Guid ParseBlobId(string raw)
    {
        // Granit.BlobStorage allocates blob ids as Guids — the BlobReference value object
        // wraps the Guid as a string at the contract boundary. A non-Guid value here means
        // the upstream provider populated BlobReference with something that did not come
        // out of IBlobStorage, which is an integration bug worth surfacing.
        if (!Guid.TryParse(raw, out Guid id))
        {
            throw new ArgumentException(
                $"BlobBackedExportItem.SourceBlob ('{raw}') is not a Guid — blob-backed export sources " +
                "expect Granit.BlobStorage references whose value is a Guid.",
                nameof(raw));
        }
        return id;
    }
}
