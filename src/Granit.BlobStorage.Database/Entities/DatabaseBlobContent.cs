using Granit.Domain;
using Granit.MultiTenancy;

namespace Granit.BlobStorage.Database.Entities;

/// <summary>
/// Stores the raw binary content of a blob in a relational database row.
/// </summary>
/// <remarks>
/// Each row represents one blob. The <see cref="ObjectKey"/> links this record
/// to the <c>BlobDescriptor</c> managed by <c>Granit.BlobStorage.EntityFrameworkCore</c>.
/// </remarks>
public sealed class DatabaseBlobContent : CreationAuditedEntity, IMultiTenant
{
    /// <summary>Tenant identifier for multi-tenant isolation. Null when tenancy is not active.</summary>
    public Guid? TenantId { get; private set; }

    /// <summary>
    /// The object key that identifies this blob in the storage abstraction.
    /// Format: <c>{tenantId}/{containerName}/{yyyy}/{MM}/{blobId}</c>.
    /// </summary>
    public string ObjectKey { get; private set; } = string.Empty;

    /// <summary>Raw binary content of the blob.</summary>
    public byte[] Content { get; private set; } = [];

    /// <summary>For EF Core materialization.</summary>
    private DatabaseBlobContent() { }

    /// <summary>
    /// Creates a new database blob content row.
    /// </summary>
    /// <param name="id">Row identifier (typically from <c>IGuidGenerator</c>).</param>
    /// <param name="objectKey">Object key linking this row to its descriptor.</param>
    /// <param name="content">Raw blob bytes.</param>
    public static DatabaseBlobContent Create(Guid id, string objectKey, byte[] content) =>
        new()
        {
            Id = id,
            ObjectKey = objectKey,
            Content = content,
        };

    Guid? IMultiTenant.TenantId
    {
        get => TenantId;
        set => TenantId = value;
    }
}
