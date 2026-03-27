using System.Diagnostics;
using Granit.BlobStorage.Diagnostics;
using Granit.BlobStorage.Domain;
using Granit.BlobStorage.Exceptions;
using Granit.BlobStorage.Options;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Timing;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.BlobStorage.Internal;

/// <summary>
/// Default orchestrator for blob storage operations.
/// Coordinates tenant resolution, key strategy, storage provider, pre-signed URL generation, and descriptor persistence.
/// </summary>
internal sealed partial class DefaultBlobStorage(
    IBlobDescriptorReader reader,
    IBlobDescriptorWriter writer,
    IBlobKeyStrategy keyStrategy,
    IBlobStoreProvider storeProvider,
    IPresignedUrlProvider presignedUrlProvider,
    IEnumerable<IBlobValidator> validators,
    IGuidGenerator guidGenerator,
    IClock clock,
    ICurrentTenant currentTenant,
    BlobStorageMetrics metrics,
    ILogger<DefaultBlobStorage> logger,
    IOptions<BlobStorageOptions> options) : IBlobStorage
{
    private const string ValidationOutcomeRejected = "rejected";

    private BlobStorageOptions Options => options.Value;

    /// <inheritdoc/>
    public async Task<PresignedUploadTicket> InitiateUploadAsync(
        string containerName,
        BlobUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        Guid blobId = guidGenerator.Create();
        string objectKey = keyStrategy.BuildObjectKey(containerName, blobId);
        string bucket = keyStrategy.ResolveBucketName(containerName);
        Guid? tenantId = currentTenant.IsAvailable ? currentTenant.Id : null;

        var descriptor = BlobDescriptor.Create(
            id: blobId,
            tenantId: tenantId,
            containerName: containerName,
            objectKey: objectKey,
            request: request,
            createdAt: clock.Now);

        await writer.SaveAsync(descriptor, cancellationToken).ConfigureAwait(false);

        PresignedUploadTicket ticket = await presignedUrlProvider.GenerateUploadTicketAsync(
            bucket,
            objectKey,
            blobId,
            request,
            Options.UploadUrlExpiry,
            cancellationToken).ConfigureAwait(false);

        metrics.RecordUploadInitiated(tenantId?.ToString(), containerName);

        return ticket;
    }

    /// <inheritdoc/>
    public async Task<PresignedDownloadUrl> CreateDownloadUrlAsync(
        string containerName,
        Guid blobId,
        DownloadUrlOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        BlobDescriptor descriptor = await FindOrThrowAsync(containerName, blobId, cancellationToken).ConfigureAwait(false);

        if (descriptor.Status != BlobStatus.Valid)
        {
            throw new BlobNotValidException(blobId, descriptor.Status);
        }

        TimeSpan expiry = options?.Expiry ?? Options.DownloadUrlExpiry;
        string bucket = keyStrategy.ResolveBucketName(containerName);

        return await presignedUrlProvider.GenerateDownloadUrlAsync(
            bucket,
            descriptor.ObjectKey,
            options,
            expiry,
            cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc/>
    public async Task<BlobDescriptor?> GetDescriptorAsync(
        string containerName,
        Guid blobId,
        CancellationToken cancellationToken = default)
    {
        BlobDescriptor? descriptor = await reader.FindAsync(blobId, cancellationToken).ConfigureAwait(false);

        if (descriptor is not null &&
            !string.Equals(descriptor.ContainerName, containerName, StringComparison.Ordinal))
        {
            return null;
        }

        return descriptor;
    }

    /// <inheritdoc/>
    public async Task DeleteAsync(
        string containerName,
        Guid blobId,
        string? deletionReason = null,
        CancellationToken cancellationToken = default)
    {
        BlobDescriptor? descriptor = await reader.FindAsync(blobId, cancellationToken).ConfigureAwait(false);

        if (descriptor is null)
        {
            throw new BlobNotFoundException(blobId, containerName);
        }

        // Idempotency: already deleted -> no-op.
        if (descriptor.Status == BlobStatus.Deleted)
        {
            return;
        }

        string bucket = keyStrategy.ResolveBucketName(containerName);

        await storeProvider.DeleteAsync(bucket, descriptor.ObjectKey, cancellationToken).ConfigureAwait(false);

        descriptor.MarkAsDeleted(clock.Now, deletionReason);
        await writer.UpdateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        metrics.RecordDeleted(descriptor.TenantId?.ToString(), containerName);
    }

    /// <inheritdoc/>
    public async Task<BlobConfirmationResult> ConfirmUploadAsync(
        string containerName, Guid blobId, CancellationToken cancellationToken = default)
    {
        BlobDescriptor descriptor = await FindOrThrowAsync(containerName, blobId, cancellationToken).ConfigureAwait(false);
        if (descriptor.Status != BlobStatus.Pending)
        {
            throw new BlobNotValidException(blobId, descriptor.Status);
        }

        descriptor.MarkAsUploading();
        string bucket = keyStrategy.ResolveBucketName(containerName);
        var stopwatch = Stopwatch.StartNew();

        long actualSize;
        try
        {
            actualSize = await storeProvider.GetSizeAsync(bucket, descriptor.ObjectKey, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            stopwatch.Stop();
            LogFileNotFound(blobId, containerName, ex);
            descriptor.MarkAsRejected("File not found in storage provider.");
            await writer.UpdateAsync(descriptor, cancellationToken).ConfigureAwait(false);
            string tenantStr = descriptor.TenantId?.ToString() ?? "global";
            metrics.RecordValidationCompleted(tenantStr, ValidationOutcomeRejected, containerName);
            metrics.RecordValidationFailed(tenantStr, "file_not_found", containerName);
            metrics.RecordConfirmDuration(tenantStr, ValidationOutcomeRejected, containerName, stopwatch.Elapsed);
            return new BlobConfirmationResult(false, BlobStatus.Rejected, null, null, "File not found in storage provider.");
        }

        var context = new BlobValidationContext
        {
            Descriptor = descriptor,
            ActualSizeBytes = actualSize,
            OpenPartialStreamAsync = (byteCount, ct) => storeProvider.OpenPartialReadAsync(bucket, descriptor.ObjectKey, byteCount, ct),
        };

        string? verifiedContentType = null;
        foreach (IBlobValidator validator in validators.OrderBy(v => v.Order))
        {
            BlobValidationResult result = await validator.ValidateAsync(context, cancellationToken).ConfigureAwait(false);
            if (!result.IsValid)
            {
                stopwatch.Stop();
                await storeProvider.DeleteAsync(bucket, descriptor.ObjectKey, cancellationToken).ConfigureAwait(false);
                descriptor.MarkAsRejected(result.FailureReason!);
                await writer.UpdateAsync(descriptor, cancellationToken).ConfigureAwait(false);
                string tenantStr = descriptor.TenantId?.ToString() ?? "global";
                metrics.RecordValidationCompleted(tenantStr, ValidationOutcomeRejected, containerName);
                metrics.RecordValidationFailed(tenantStr, result.FailureReason!, containerName);
                metrics.RecordConfirmDuration(tenantStr, ValidationOutcomeRejected, containerName, stopwatch.Elapsed);
                return new BlobConfirmationResult(false, BlobStatus.Rejected, null, null, result.FailureReason);
            }

            verifiedContentType ??= result.VerifiedContentType;
        }

        stopwatch.Stop();
        verifiedContentType ??= descriptor.DeclaredContentType;
        descriptor.MarkAsValid(verifiedContentType, actualSize, clock.Now);
        await writer.UpdateAsync(descriptor, cancellationToken).ConfigureAwait(false);

        string validTenant = descriptor.TenantId?.ToString() ?? "global";
        metrics.RecordValidationCompleted(validTenant, "valid", containerName);
        metrics.RecordConfirmDuration(validTenant, "valid", containerName, stopwatch.Elapsed);

        return new BlobConfirmationResult(true, BlobStatus.Valid, verifiedContentType, actualSize, null);
    }

    /// <inheritdoc/>
    public async Task<int> CleanupOrphansAsync(CancellationToken cancellationToken = default)
    {
        DateTimeOffset cutoff = clock.Now.AddHours(-24);
        IReadOnlyList<BlobDescriptor> orphans = await reader.FindOrphanedAsync(cutoff, batchSize: 100, cancellationToken).ConfigureAwait(false);

        int cleaned = 0;
        foreach (BlobDescriptor descriptor in orphans)
        {
            string bucket = keyStrategy.ResolveBucketName(descriptor.ContainerName);
            try
            {
                await storeProvider.DeleteAsync(bucket, descriptor.ObjectKey, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                LogOrphanDeleteFailed(descriptor.Id, ex);
            }

            if (descriptor.Status == BlobStatus.Pending)
            {
                descriptor.MarkAsUploading();
            }
            descriptor.MarkAsRejected("Orphan cleanup");
            await writer.UpdateAsync(descriptor, cancellationToken).ConfigureAwait(false);
            metrics.RecordOrphanCleaned(descriptor.TenantId?.ToString());
            cleaned++;
        }

        return cleaned;
    }

    private async Task<BlobDescriptor> FindOrThrowAsync(
        string containerName, Guid blobId, CancellationToken cancellationToken)
    {
        BlobDescriptor? descriptor = await reader.FindAsync(blobId, cancellationToken).ConfigureAwait(false);
        return descriptor ?? throw new BlobNotFoundException(blobId, containerName);
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Blob {BlobId} in container '{ContainerName}' not found on storage provider during confirm.")]
    private partial void LogFileNotFound(Guid blobId, string containerName, Exception exception);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Failed to delete storage object for orphaned blob {BlobId} during cleanup.")]
    private partial void LogOrphanDeleteFailed(Guid blobId, Exception exception);
}
