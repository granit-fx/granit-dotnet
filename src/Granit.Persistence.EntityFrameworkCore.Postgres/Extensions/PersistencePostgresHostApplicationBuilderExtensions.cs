using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.MultiTenancy;
using Granit.Persistence.EntityFrameworkCore.Postgres.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Granit.Persistence.EntityFrameworkCore.Postgres.Extensions;

/// <summary>
/// Extension methods for registering PostgreSQL-specific persistence services.
/// </summary>
public static class PersistencePostgresHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers PostgreSQL-specific persistence services:
    /// <list type="bullet">
    ///   <item><see cref="IGranitMigrationLock"/> — <c>pg_try_advisory_lock</c>-backed distributed migration lock.</item>
    ///   <item><see cref="ITenantSchemaActivator"/> — executes <c>SET search_path</c> per tenant.</item>
    ///   <item><see cref="ITenantDbIsolator"/> — isolates a <c>DbContext</c> to a tenant schema before migrations.</item>
    /// </list>
    /// </summary>
    /// <remarks>
    /// The migration lock uses <c>AddSingleton</c> (replace), so it wins over the
    /// <c>NullMigrationLock</c> fallback regardless of registration order. The schema
    /// activator and tenant isolator remain <c>TryAdd</c> — call this before any
    /// <c>AddGranitTenantPerSchemaDbContext</c> call so those are not overridden by
    /// no-op fallbacks.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitPostgres(
        this IHostApplicationBuilder builder)
    {
        // Register the Npgsql provider factory so NpgsqlAdvisoryMigrationLock can
        // resolve it via DbProviderFactories.TryGetFactory("Npgsql", ...).
        DbProviderFactories.RegisterFactory("Npgsql", NpgsqlFactory.Instance);

        // AddSingleton (replace): the real distributed lock must win over the
        // NullMigrationLock fallback whatever the registration order.
        builder.Services.AddSingleton<IGranitMigrationLock, NpgsqlAdvisoryMigrationLock>();
        builder.Services.TryAddSingleton<ITenantSchemaActivator, NpgsqlTenantSchemaActivator>();
        builder.Services.TryAddSingleton<ITenantDbIsolator, NpgsqlTenantDbIsolator>();
        return builder;
    }
}
