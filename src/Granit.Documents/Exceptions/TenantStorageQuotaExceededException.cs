namespace Granit.Documents.Exceptions;

/// <summary>
/// Thrown by the document upload pipeline when a finalisation would push the tenant past
/// its <c>TenantStorageQuota.LimitBytes</c> (F7.2). Endpoints translate this into an
/// HTTP 403 RFC-7807 problem document with
/// <c>type=https://granit.dev/problems/quota-exceeded</c>.
/// </summary>
public sealed class TenantStorageQuotaExceededException(
    Guid tenantId,
    long requestedBytes,
    string? message = null)
    : InvalidOperationException(
        message ?? $"Tenant '{tenantId}' would exceed its storage quota by accepting {requestedBytes} additional bytes.")
{
    /// <summary>Identifier of the tenant whose quota would be exceeded.</summary>
    public Guid TenantId { get; } = tenantId;

    /// <summary>The number of bytes the rejected upload would have consumed.</summary>
    public long RequestedBytes { get; } = requestedBytes;
}
