using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods to register the Granit Identity EF Core services.
/// </summary>
public static class IdentityEntityFrameworkCoreServiceCollectionExtensions
{
    /// <summary>
    /// Registers the <see cref="IdentityDbContext"/> + the EF Core
    /// implementation of <see cref="IUserDirectoryQueryableSource"/>.
    /// Hosts call this from their <c>Program.cs</c> alongside the other
    /// <c>AddGranit*EntityFrameworkCore</c> companions; the
    /// <paramref name="configureDbContext"/> callback supplies the
    /// connection string and provider (Npgsql in production, SQLite for
    /// in-memory tests).
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configureDbContext">Configures <see cref="DbContextOptionsBuilder"/> (connection string + provider).</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddGranitIdentityEntityFrameworkCore(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureDbContext)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureDbContext);

        services.AddGranitDbContext<IdentityDbContext>(configureDbContext);

        services.TryAddScoped<IUserDirectoryQueryableSource, EfUserDirectoryQueryableSource>();

        return services;
    }
}
