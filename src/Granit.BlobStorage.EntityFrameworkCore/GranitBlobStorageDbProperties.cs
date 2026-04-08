using Granit.Persistence.EntityFrameworkCore;

namespace Granit.BlobStorage.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the BlobStorage EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// These properties control the table prefix and schema used by all entity configurations
/// in <c>Granit.BlobStorage.EntityFrameworkCore</c>. Both the internal
/// <c>BlobStorageDbContext</c> and the host's <c>Configure*Module()</c> call read
/// the same static values, ensuring migration-time and runtime SQL stay in sync.
/// </para>
/// <para>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled <see cref="Microsoft.EntityFrameworkCore.Metadata.IModel"/>
/// after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitBlobStorageDbProperties
{
    /// <summary>
    /// Table name prefix for all blob storage tables. Default: <c>"blob_storage_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "blob_storage_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for tenant-level tables.
    /// Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
