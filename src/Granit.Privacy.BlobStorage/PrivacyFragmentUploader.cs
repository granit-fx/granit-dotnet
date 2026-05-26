using System.Security.Cryptography;
using System.Text;
using Granit.Domain.ValueObjects;
using Granit.Events;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Audit;
using Granit.Privacy.DataExport.Events;
using Granit.Privacy.DataExport.Fragments;
using Microsoft.Extensions.Logging;

namespace Granit.Privacy.BlobStorage;

/// <summary>
/// Iterates the <see cref="ExportFragment"/> stream yielded by an
/// <see cref="IPrivacyDataProvider"/> and publishes one
/// <see cref="PersonalDataPreparedEto"/> per fragment. Empty providers emit a single
/// sentinel event so the saga can decrement its pending-providers set without leaving an
/// orphan entry in the manifest.
/// </summary>
/// <remarks>
/// Provider-side Wolverine handlers stay one-liners — all the iteration / sentinel logic
/// lives here, where it can be unit-tested in isolation. The actual staging upload (for
/// <see cref="StagedExportFragment"/>) and HMAC signing happen inside the provider via
/// <see cref="IStagedFragmentBuilder"/> before the fragment reaches the uploader.
/// </remarks>
public sealed partial class PrivacyFragmentUploader(
    IDistributedEventBus eventBus,
    IPrivacyExportAuditWriter auditWriter,
    TimeProvider timeProvider,
    ILogger<PrivacyFragmentUploader> logger)
{
    /// <summary>FragmentKind sentinel value for providers with no data for the subject.</summary>
    public const string EmptyFragmentKind = "empty";

    /// <summary>FragmentKind value for staged fragments (bytes already in staging container).</summary>
    public const string StagedFragmentKind = "staged";

    /// <summary>FragmentKind value for pass-through fragments (source blob, single-transit).</summary>
    public const string PassThroughFragmentKind = "passthrough";

    /// <summary>
    /// Iterates the provider's fragment stream and publishes the corresponding events.
    /// </summary>
    public async Task UploadAsync<TProvider>(
        PersonalDataRequestedEto request,
        TProvider provider,
        CancellationToken cancellationToken)
        where TProvider : class, IPrivacyDataProvider
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(provider);

        var context = new PrivacyExportContext(
            RequestId: request.RequestId,
            SubjectUserId: request.UserId,
            CallerUserId: request.UserId,
            TenantId: request.TenantId,
            Regulation: request.Regulation);

        int published = 0;
        await foreach (ExportFragment fragment in provider
            .ExportAsync(context, cancellationToken)
            .ConfigureAwait(false))
        {
            (string kind, string container, BlobReference blob) = fragment switch
            {
                StagedExportFragment staged => (StagedFragmentKind, PrivacyExportContainerNames.FragmentContainer, staged.StagedBlob),
                PassThroughExportFragment pt => (PassThroughFragmentKind, pt.SourceContainer, pt.SourceBlob),
                _ => throw new InvalidOperationException($"Unknown fragment kind: {fragment.GetType().Name}"),
            };

            await eventBus.PublishAsync(
                new PersonalDataPreparedEto(
                    RequestId: request.RequestId,
                    ProviderName: TProvider.ProviderName,
                    FragmentKind: kind,
                    SourceContainer: container,
                    BlobReferenceId: blob,
                    EntryPath: fragment.EntryPath,
                    ContentType: fragment.ContentType,
                    IntegrityTag: fragment.IntegrityTag,
                    TenantId: request.TenantId),
                cancellationToken).ConfigureAwait(false);

            LogFragmentPrepared(logger, TProvider.ProviderName, request.UserId, request.RequestId, fragment.EntryPath, kind);

            await auditWriter.WriteFragmentPreparedAsync(
                new PrivacyExportFragmentPreparedAudit(
                    RequestId: request.RequestId,
                    SubjectUserId: request.UserId,
                    TenantId: request.TenantId,
                    ProviderName: TProvider.ProviderName,
                    FragmentKind: kind,
                    EntryPathHash: HashEntryPath(fragment.EntryPath),
                    SizeBytes: fragment.KnownSizeBytes,
                    Timestamp: timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);

            published++;
        }

        if (published == 0)
        {
            LogEmptyFragment(logger, TProvider.ProviderName, request.UserId, request.RequestId);
            await eventBus.PublishAsync(
                new PersonalDataPreparedEto(
                    RequestId: request.RequestId,
                    ProviderName: TProvider.ProviderName,
                    FragmentKind: EmptyFragmentKind,
                    SourceContainer: PrivacyExportContainerNames.FragmentContainer,
                    BlobReferenceId: BlobReference.Create($"{PrivacyExportContainerNames.EmptyFragmentPrefix}{request.RequestId}"),
                    EntryPath: $"{TProvider.ProviderName}.empty",
                    ContentType: "application/octet-stream",
                    IntegrityTag: string.Empty,
                    TenantId: request.TenantId),
                cancellationToken).ConfigureAwait(false);

            await auditWriter.WriteFragmentPreparedAsync(
                new PrivacyExportFragmentPreparedAudit(
                    RequestId: request.RequestId,
                    SubjectUserId: request.UserId,
                    TenantId: request.TenantId,
                    ProviderName: TProvider.ProviderName,
                    FragmentKind: EmptyFragmentKind,
                    EntryPathHash: HashEntryPath($"{TProvider.ProviderName}.empty"),
                    SizeBytes: null,
                    Timestamp: timeProvider.GetUtcNow()),
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static string HashEntryPath(string entryPath)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(entryPath));
        return Convert.ToHexStringLower(hash);
    }

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export: provider {Provider} has no data for user {UserId} (request {RequestId}); emitting empty sentinel")]
    private static partial void LogEmptyFragment(ILogger logger, string provider, Guid userId, Guid requestId);

    [LoggerMessage(Level = LogLevel.Information,
        Message = "Privacy export: provider {Provider} yielded fragment {EntryPath} ({Kind}) for user {UserId} (request {RequestId})")]
    private static partial void LogFragmentPrepared(ILogger logger, string provider, Guid userId, Guid requestId, string entryPath, string kind);
}
