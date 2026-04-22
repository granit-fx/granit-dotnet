using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.OpenIddict.Entities.OpenIddict;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.OpenIddict.Options;
using Granit.OpenIddict.Server.Extensions;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.OpenIddict.EntityFrameworkCore.Extensions;

/// <summary>
/// Extension methods for registering OpenIddict EF Core persistence in the host application.
/// </summary>
[ExcludeFromCodeCoverage]
public static class OpenIddictEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Registers the complete Granit OpenIddict stack: ASP.NET Core Identity, OpenIddict
    /// (core + server + validation), and EF Core persistence.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(cs)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitOpenIddict(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // Read options from configuration for build-time decisions
        GranitOpenIddictOptions granitOptions = new();
        builder.Configuration.GetSection("OpenIddict").Bind(granitOptions);

        // 1. Register the isolated DbContext with Granit interceptors
        builder.Services.AddGranitDbContext<OpenIddictDbContext>(configure);

        // 2. Bind lockout options (exponential backoff)
        GranitLockoutOptions lockoutOptions = new();
        builder.Configuration.GetSection(GranitLockoutOptions.SectionName).Bind(lockoutOptions);
        builder.Services.Configure<GranitLockoutOptions>(
            builder.Configuration.GetSection(GranitLockoutOptions.SectionName));

        // 3. Register ASP.NET Core Identity
        builder.Services
            .AddIdentity<GranitUser, GranitRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = lockoutOptions.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = lockoutOptions.BaseLockoutDuration;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<OpenIddictDbContext>()
            .AddDefaultTokenProviders();

        // 3. Register OpenIddict Core — EF Core stores
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

        // 4. Register OpenIddict Server + Validation (delegated to Granit.OpenIddict.Server)
        builder.AddGranitOpenIddictServer();

        // 5. Register group store — required by AspNetIdentityProvider
        builder.Services.TryAddScoped<ILocalIdentityGroupStore, OpenIddictGroupStore>();

        // 6. Accessor exposing the scoped OpenIddictDbContext as IIdentityDbContextAccessor —
        //    consumed by IGranitRoleOrchestrator to share the Identity transaction
        //    with the host authorization DbContext when both target the same database.
        builder.Services.TryAddScoped<IIdentityDbContextAccessor,
            OpenIddictIdentityDbContextAccessor>();

        return builder;
    }
}
