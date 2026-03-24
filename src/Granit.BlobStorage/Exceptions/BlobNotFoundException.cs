using Granit.Exceptions;

namespace Granit.BlobStorage.Exceptions;

/// <summary>
/// Thrown when a <see cref="BlobDescriptor"/> is not found for the current tenant.
/// Maps to <c>404 Not Found</c>.
/// </summary>
/// <remarks>
/// Implements <see cref="IHasErrorCode"/>: the title sent to the client is resolved
/// from the <c>BlobStorage</c> localization resource (key <c>"BlobStorage:NotFound"</c>).
/// The detailed message (blobId, containerName) is available for structured logging only.
/// </remarks>
public sealed class BlobNotFoundException : NotFoundException, IHasErrorCode
{
    /// <summary>The identifier of the blob that was not found.</summary>
    public Guid BlobId { get; }

    /// <summary>The container in which the lookup was performed.</summary>
    public string ContainerName { get; }

    /// <inheritdoc/>
    public string ErrorCode => "BlobStorage:NotFound";

    /// <summary>
    /// Initializes a new instance of <see cref="BlobNotFoundException"/>.
    /// </summary>
    /// <param name="blobId">Identifier of the missing blob.</param>
    /// <param name="containerName">Name of the container.</param>
    public BlobNotFoundException(Guid blobId, string containerName)
        : base($"Blob '{blobId}' not found in container '{containerName}' for the current tenant.")
    {
        BlobId = blobId;
        ContainerName = containerName;
    }
}
