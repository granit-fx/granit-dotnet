using System.Diagnostics.CodeAnalysis;
using Granit.DataExchange.Export;
using Granit.Identity.Local.EntityFrameworkCore.Extensions;
using Granit.OpenIddict.EntityFrameworkCore.Entities;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Extensions;
using Granit.OpenIddict.Models;
using Granit.OpenIddict.Options;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.QueryEngine;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.OpenIddict.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict EF Core persistence in the host application.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the persistence + identity layer for OpenIddict: the isolated
    /// <see cref="OpenIddictDbContext"/>, ASP.NET Core Identity backed by it, OpenIddict Core with
    /// EF Core stores, the group store and DbContext accessor, the session/device provider, and
    /// the query engine / export sources for applications and scopes.
    /// </summary>
    /// <remarks>
    /// Does NOT wire the OpenIddict server pipeline — that is added on top by
    /// <c>Granit.Bundle.OpenIddict</c> (<c>AddGranitOpenIddict</c>), so this package carries no
    /// dependency on <c>Granit.OpenIddict.Server</c>. A migration host or admin tool that needs
    /// only the schema, stores, and identity can reference this package without the server.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(cs)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddictEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Read options from configuration for build-time decisions
        GranitOpenIddictOptions granitOptions = new();
        builder.Configuration.GetSection(GranitOpenIddictOptions.SectionName).Bind(granitOptions);

        // 0. Local-identity persistence (its own isolated IdentityLocalDbContext + ASP.NET Core
        //    Identity stores + query sources), configured against the same database/provider.
        builder.AddGranitIdentityLocalEntityFrameworkCore(configure);

        // 1. Register the isolated OpenIddict DbContext with Granit interceptors
        builder.Services.AddGranitDbContext<OpenIddictDbContext>(configure);

        // 4. Register OpenIddict Core — EF Core stores
        builder.Services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseEntityFrameworkCore()
                    .UseDbContext<OpenIddictDbContext>()
                    .ReplaceDefaultEntities<GranitOpenIddictApplication,
                        GranitOpenIddictAuthorization,
                        GranitOpenIddictScope,
                        GranitOpenIddictToken, Guid>();

                // Disable entity caching unless explicitly opted in (single-tenant deployments).
                // Our custom entities implement IMultiTenant — the cache uses ClientId as sole
                // key, which would bypass tenant filters.
                if (!granitOptions.EnableEntityCaching)
                {
                    options.DisableEntityCaching();
                }
            });

        // 7. Session/device provider for the canonical /sessions + /devices API. Registered here so a
        //    host cannot enable OpenIddict persistence and still silently serve the no-op session
        //    defaults (the failure mode when GranitOpenIddictModule is absent from the graph).
        builder.Services.AddOpenIddictUserSessionProvider();

        // 8. Query engine + export sources for the OpenIddict-owned entities.
        // The OpenIddict application/scope sources project the EF entity onto the framework-owned
        // model, so they back BOTH the query engine (IQueryableSource) and exports (IExportDataSource,
        // whose DbSet-discovering fallback cannot resolve a projection record). Register the concrete
        // once and forward both interfaces to the same scoped instance.
        builder.Services.AddScoped<EfGranitOpenIddictApplicationQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<OpenIddictApplicationModel>>(
            sp => sp.GetRequiredService<EfGranitOpenIddictApplicationQueryableSource>());
        builder.Services.AddScoped<IExportDataSource<OpenIddictApplicationModel>>(
            sp => sp.GetRequiredService<EfGranitOpenIddictApplicationQueryableSource>());

        builder.Services.AddScoped<EfGranitOpenIddictScopeQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<OpenIddictScopeModel>>(
            sp => sp.GetRequiredService<EfGranitOpenIddictScopeQueryableSource>());
        builder.Services.AddScoped<IExportDataSource<OpenIddictScopeModel>>(
            sp => sp.GetRequiredService<EfGranitOpenIddictScopeQueryableSource>());

        return builder;
    }
}
