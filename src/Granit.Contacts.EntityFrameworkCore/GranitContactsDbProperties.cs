using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore;

/// <summary>Table-naming properties for the Contacts EF Core module.</summary>
public static class GranitContactsDbProperties
{
    /// <summary>Table prefix. Default: <c>"contacts_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "contacts_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema. Falls back to <see cref="GranitDbDefaults.HostDbSchema"/>,
    /// then <see cref="GranitDbDefaults.DbSchema"/> when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
