using Granit.Persistence.EntityFrameworkCore.Hosting;
using Granit.Persistence.EntityFrameworkCore.SqlServer.Internal;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
    /// Call this before <c>AddGranitMigrateSupport()</c> so that
    /// <c>TryAddSingleton&lt;IGranitMigrationLock, NullMigrationLock&gt;()</c>
    /// is a no-op and the application lock is used instead.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitSqlServer(
        this IHostApplicationBuilder builder)
    {
        builder.Services.TryAddSingleton<IGranitMigrationLock, SqlServerAppLock>();
        return builder;
    }
}
