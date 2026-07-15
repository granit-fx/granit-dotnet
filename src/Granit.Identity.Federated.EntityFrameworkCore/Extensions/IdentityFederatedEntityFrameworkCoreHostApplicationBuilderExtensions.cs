using Granit.Identity.Federated.EntityFrameworkCore.Internal;
using Granit.Identity.Federated.Internal;
using Granit.Identity.Federated.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Identity.Federated.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for enabling EF Core persistence in Granit.Identity.Federated.
/// </summary>
public static class IdentityFederatedEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Replaces the default in-memory / null stores with durable EF Core implementations
    /// backed by a dedicated <see cref="IdentityFederatedDbContext"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Must be called after <c>AddGranitIdentityFederated()</c>. Registers:
    /// </para>
    /// <list type="bullet">
    ///   <item><see cref="CachedUserLookupService"/> — replaces the default <c>NullUserLookupService</c>.</item>
    ///   <item><see cref="EfCoreUserCacheStore"/> — implements <c>IUserCacheStore</c>.</item>
    ///   <item><c>UserCacheStats</c> — the default <c>IUserCacheStats</c> implementation.</item>
    ///   <item><see cref="FederatedUserCacheEraserAdapter"/> — implements <c>IFederatedUserCacheEraser</c>.</item>
    ///   <item><see cref="IdentityFederatedDbContext"/> — registered as <c>IDbContextFactory&lt;T&gt;</c> for thread-safe usage in Wolverine handlers.</item>
    /// </list>
    /// <para>
    /// Promotes the module out of the legacy interface-only pattern (<c>IUserCacheDbContext</c>),
    /// matching the dedicated-DbContext convention adopted across the framework. Per ADR-063
    /// the module is dual-scope row-level — host-federated and tenant-federated identities
    /// coexist in this single DbContext with row-level <c>IMultiTenant</c> filtering.
    /// </para>
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core <see cref="DbContextOptionsBuilder"/> configuration (provider + connection string).</param>
    /// <returns>The builder for chaining.</returns>
    /// <exception cref="ArgumentNullException">When <paramref name="builder"/> or <paramref name="configure"/> is <c>null</c>.</exception>
    public static IHostApplicationBuilder AddGranitIdentityFederatedEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        builder.Services.AddGranitDbContext<IdentityFederatedDbContext>(configure);

        builder.Services.Replace(ServiceDescriptor.Scoped<IUserLookupService, CachedUserLookupService>());
        builder.Services.Replace(ServiceDescriptor.Scoped<IUserCacheStats, UserCacheStats>());
        builder.Services.TryAddScoped<IUserCacheStore, EfCoreUserCacheStore>();
        // The single joint-hydration write path (ADR-051), shared by the cache-aside service,
        // the provider webhook handler, and the login-time sync middleware.
        builder.Services.TryAddScoped<IFederatedIdentityWriter, FederatedIdentityWriter>();
        builder.Services.TryAddScoped<IFederatedUserCacheReader, FederatedUserCacheReaderAdapter>();
        builder.Services.TryAddScoped<IFederatedUserCacheEraser, FederatedUserCacheEraserAdapter>();
        builder.Services.AddOptions<UserCacheOptions>()
            .BindConfiguration(UserCacheOptions.SectionName);

        return builder;
    }
}
