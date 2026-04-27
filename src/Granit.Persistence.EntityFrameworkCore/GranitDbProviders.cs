namespace Granit.Persistence.EntityFrameworkCore;

/// <summary>
/// Database-provider name identifiers as exposed by EF Core
/// (<see cref="Microsoft.EntityFrameworkCore.Infrastructure.DatabaseFacade.ProviderName"/>,
/// <see cref="Microsoft.EntityFrameworkCore.Migrations.MigrationBuilder.ActiveProvider"/>,
/// <c>DbContext.Database.ProviderName</c>, etc.).
/// </summary>
/// <remarks>
/// <para>
/// Use these constants to write provider-conditional code without scattering magic strings
/// across the codebase. Typical pattern:
/// <code>
/// if (db.Database.ProviderName == GranitDbProviders.Postgres)
/// {
///     await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(...)", ct);
/// }
/// </code>
/// </para>
/// <para>
/// Currently consumed by <c>MergeableConcurrencyLock</c>, <c>MeteringConcurrencyLock</c>,
/// and <c>PartiesPostgresMigrationExtensions</c>. Keep this list narrow — adding a provider
/// here implies the framework has tested support for it; only PostgreSQL and SQL Server are
/// first-class today.
/// </para>
/// </remarks>
public static class GranitDbProviders
{
    /// <summary>The Npgsql provider for PostgreSQL.</summary>
    public const string Postgres = "Npgsql.EntityFrameworkCore.PostgreSQL";

    /// <summary>The Microsoft provider for SQL Server.</summary>
    public const string SqlServer = "Microsoft.EntityFrameworkCore.SqlServer";
}
