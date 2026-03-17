using Granit.Core.Domain;

namespace Granit.BlobStorage.DbStore.Entities;

/// <summary>
/// Stores the raw binary content of a blob in a relational database row.
/// </summary>
/// <remarks>
/// Each row represents one blob. The <see cref="ObjectKey"/> links this record
/// to the <c>BlobDescriptor</c> managed by <c>Granit.BlobStorage.EntityFrameworkCore</c>.
/// </remarks>
public sealed class DbStoreBlobContent : IMultiTenant
{
    /// <summary>Primary key.</summary>
    public Guid Id { get; set; }

    /// <summary>Tenant identifier for multi-tenant isolation. Null when tenancy is not active.</summary>
    public Guid? TenantId { get; set; }

    /// <summary>
    /// The object key that identifies this blob in the storage abstraction.
    /// Format: <c>{tenantId}/{containerName}/{yyyy}/{MM}/{blobId}</c>.
    /// </summary>
    public string ObjectKey { get; set; } = string.Empty;

    /// <summary>Raw binary content of the blob.</summary>
    public byte[] Content { get; set; } = [];

    /// <summary>Timestamp when the blob was stored.</summary>
    public DateTimeOffset CreatedAt { get; set; }
}
