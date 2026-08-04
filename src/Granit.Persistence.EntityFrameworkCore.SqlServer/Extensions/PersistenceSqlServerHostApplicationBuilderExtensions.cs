using System.Data.Common;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Persistence.EntityFrameworkCore.SqlServer.Extensions;

/// <summary>
/// Extension methods for registering SQL Server-specific persistence services.
/// </summary>
public static class PersistenceSqlServerHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers SQL Server-specific persistence services, including the
    /// <see cref="IGranitMigrationLock"/> implementation backed by
    /// <c>sp_getapplock</c>.
    /// </summary>
    /// <remarks>
    /// Uses <c>AddSingleton</c> (replace) for the lock so the real distributed lock wins
    /// over the <c>NullMigrationLock</c> fallback regardless of whether this method is
    /// called before or after <c>AddGranitMigrateSupport()</c> /
    /// <c>AddGranitPersistenceMigrations()</c>.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitSqlServer(
        this IHostApplicationBuilder builder)
    {
        // Register the SqlClient provider factory so SqlServerAppLock can resolve it via
        // DbProviderFactories.TryGetFactory("Microsoft.Data.SqlClient", ...). Without this,
        // every SQL Server host silently fell back to a no-op lock handle and ran
        // migrations without any distributed lock (mirror of the Npgsql registration in
        // AddGranitPostgres).
        DbProviderFactories.RegisterFactory("Microsoft.Data.SqlClient", SqlClientFactory.Instance);

        builder.Services.AddSingleton<IGranitMigrationLock, SqlServerAppLock>();
        return builder;
    }
}
