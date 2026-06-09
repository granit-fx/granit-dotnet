using Granit.Persistence.EntityFrameworkCore;

namespace Granit.BlobStorage.Database;

/// <summary>
/// Provides configurable table-naming properties for the BlobStorage Database provider.
/// </summary>
/// <remarks>
/// <b>Important:</b> Set these properties at application startup, before
/// <c>ConfigureServices</c> completes. EF Core caches the compiled model
/// after first use — later mutations have no effect.
/// </remarks>
public static class GranitBlobStorageDatabaseDbProperties
{
    /// <summary>
    /// Table name prefix for all BlobStorage Database tables. Default: <c>"blob_storage_"</c>.
    /// </summary>
    public static string DbTablePrefix { get; set; } = "blob_storage_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for BlobStorage Database tables.
    /// Falls back to <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
