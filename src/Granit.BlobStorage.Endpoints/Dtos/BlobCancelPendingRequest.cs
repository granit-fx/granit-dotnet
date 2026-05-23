namespace Granit.BlobStorage.Endpoints.Dtos;

/// <summary>
/// Request to cancel a Pending upload whose presigned PUT failed client-side.
/// </summary>
/// <param name="ContainerName">Logical container the blob belongs to.</param>
/// <param name="Reason">Human-readable cancellation reason for the audit trail (e.g. <c>"PUT returned 400 SignatureDoesNotMatch"</c>).</param>
public sealed record BlobCancelPendingRequest(string ContainerName, string Reason);
