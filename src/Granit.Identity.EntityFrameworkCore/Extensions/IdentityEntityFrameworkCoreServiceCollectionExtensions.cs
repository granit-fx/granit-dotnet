using Granit.Identity.EntityFrameworkCore.Internal;
using Granit.Identity.Internal;
using Granit.Identity.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.Interceptors;
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
    /// implementations of <see cref="IUserDirectoryQueryableSource"/>
    /// and <see cref="IUserDirectoryWriter"/>. Hosts call this from
    /// their <c>Program.cs</c> alongside the other
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
        services.TryAddScoped<IUserDirectoryWriter, EfUserDirectoryWriter>();

        // Lookup hasher backing User.EmailHash / User.PhoneNumberHash. Pepper
        // validated at first resolution (HmacUserLookupHasher ctor) — fail-fast
        // on missing configuration so production deployments cannot run with a
        // known-zero key.
        services.AddOptions<UserLookupHasherOptions>()
            .BindConfiguration(UserLookupHasherOptions.SectionName);
        services.TryAddSingleton<IUserLookupHasher, HmacUserLookupHasher>();

        // Save-time interceptor that recomputes the digests in lockstep with
        // the encrypted plaintext columns. Registered via IGranitAutoInterceptor
        // (same pattern as AuditingChangeTrackingInterceptor) so any DbContext
        // wired through AddGranitDbContext picks it up.
        services.AddScoped<UserLookupHashInterceptor>();
        services.AddScoped<IGranitAutoInterceptor>(sp =>
            sp.GetRequiredService<UserLookupHashInterceptor>());

        return services;
    }
}
