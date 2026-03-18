using Granit.Persistence.Hosting;
using Granit.Persistence.Postgres.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Persistence.Postgres.Extensions;

/// <summary>
/// Extension methods for registering PostgreSQL-specific persistence services.
/// </summary>
public static class PersistencePostgresHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers PostgreSQL-specific persistence services, including the
    /// <see cref="IGranitMigrationLock"/> implementation backed by
    /// <c>pg_try_advisory_lock</c>.
    /// </summary>
    /// <remarks>
    /// Call this before <c>AddGranitMigrateSupport()</c> so that
    /// <c>TryAddSingleton&lt;IGranitMigrationLock, NullMigrationLock&gt;()</c>
    /// is a no-op and the advisory lock is used instead.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitPostgres(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IGranitMigrationLock, NpgsqlAdvisoryMigrationLock>();
        return builder;
    }
}
