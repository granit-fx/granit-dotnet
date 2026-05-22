using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Presence.EntityFrameworkCore;

/// <summary>
/// Provides configurable table-naming properties for the Presence EF Core module.
/// </summary>
/// <remarks>
/// <para>
/// Presence is a single-table module. The table lives in the host schema because the
/// <see cref="Granit.Presence.Domain.UserPresence"/> aggregate is global per user — it is
/// NOT multi-tenant scoped (a human has a single presence across every tenant).
/// </para>
/// <para>
/// Set these properties at application startup, before <c>ConfigureServices</c> completes.
/// EF Core caches the compiled model after first use — later mutations have no effect.
/// </para>
/// </remarks>
public static class GranitPresenceDbProperties
{
    /// <summary>Table name prefix for the presence table. Default: <c>"presence_"</c>.</summary>
    public static string DbTablePrefix { get; set; } = "presence_";

    private static string? _dbSchema;
    private static bool _dbSchemaExplicitlySet;

    /// <summary>
    /// Database schema for the presence table. Falls back to
    /// <see cref="GranitDbDefaults.HostDbSchema"/>, then <see cref="GranitDbDefaults.DbSchema"/>
    /// when not explicitly set.
    /// </summary>
    public static string? DbSchema
    {
        get => _dbSchemaExplicitlySet ? _dbSchema : GranitDbDefaults.HostDbSchema ?? GranitDbDefaults.DbSchema;
        set { _dbSchema = value; _dbSchemaExplicitlySet = true; }
    }
}
