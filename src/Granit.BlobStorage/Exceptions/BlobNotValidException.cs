using Granit.BlobStorage.Domain;
using Granit.Exceptions;

namespace Granit.BlobStorage.Exceptions;

/// <summary>
/// Thrown when a download URL is requested for a blob that is not in <see cref="BlobStatus.Valid"/> state.
/// Maps to <c>400 Bad Request</c>.
/// </summary>
/// <remarks>
/// Implements <see cref="IHasErrorCode"/>: the title sent to the client is resolved
/// from the <c>BlobStorage</c> localization resource (key <c>"BlobStorage:NotValid"</c>).
/// The current status is exposed as a structured property for logging; it is not forwarded
/// to the client response.
/// </remarks>
public sealed class BlobNotValidException : Exception, IHasErrorCode
{
    /// <summary>The identifier of the blob.</summary>
    public Guid BlobId { get; }

    /// <summary>The actual status of the blob at the time of the failed download attempt.</summary>
    public BlobStatus CurrentStatus { get; }

    /// <inheritdoc/>
    public string ErrorCode => "BlobStorage:NotValid";

    /// <summary>
    /// Initializes a new instance of <see cref="BlobNotValidException"/>.
    /// </summary>
    /// <param name="blobId">Identifier of the blob.</param>
    /// <param name="currentStatus">Actual status of the blob.</param>
    public BlobNotValidException(Guid blobId, BlobStatus currentStatus)
        : base($"Blob '{blobId}' cannot be downloaded: current status is '{currentStatus}' (expected '{BlobStatus.Valid}').")
    {
        BlobId = blobId;
        CurrentStatus = currentStatus;
    }
}
