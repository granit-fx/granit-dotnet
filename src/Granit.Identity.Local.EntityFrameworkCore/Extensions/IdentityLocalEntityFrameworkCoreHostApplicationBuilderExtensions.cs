using System.Diagnostics.CodeAnalysis;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.EntityFrameworkCore.Internal;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.Persistence.EntityFrameworkCore.Extensions;
using Granit.Persistence.EntityFrameworkCore.SharedConnection;
using Granit.QueryEngine;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Granit.Identity.Local.EntityFrameworkCore.Extensions;

/// <summary>
/// Registers the local-identity persistence layer: the isolated <see cref="IdentityLocalDbContext"/>,
/// ASP.NET Core Identity backed by it, the DbContext accessor for cross-module transaction sharing,
/// and the query-engine sources for roles and user groups.
/// </summary>
[ExcludeFromCodeCoverage]
public static class IdentityLocalEntityFrameworkCoreHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the local-identity DbContext, ASP.NET Core Identity stores and query sources.
    /// </summary>
    /// <param name="builder">The host application builder.</param>
    /// <param name="configure">EF Core provider configuration (e.g. <c>options.UseNpgsql(cs)</c>).</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitIdentityLocalEntityFrameworkCore(
        this IHostApplicationBuilder builder,
        Action<DbContextOptionsBuilder> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);

        // 1. Isolated DbContext with Granit interceptors.
        builder.Services.AddGranitDbContext<IdentityLocalDbContext>(configure);

        // 2. Lockout options (exponential backoff).
        GranitLockoutOptions lockoutOptions = new();
        builder.Configuration.GetSection(GranitLockoutOptions.SectionName).Bind(lockoutOptions);
        builder.Services.Configure<GranitLockoutOptions>(
            builder.Configuration.GetSection(GranitLockoutOptions.SectionName));

        // 3. ASP.NET Core Identity backed by the local-identity DbContext.
        builder.Services
            .AddIdentity<LocalIdentity, GranitRole>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.Lockout.MaxFailedAccessAttempts = lockoutOptions.MaxFailedAccessAttempts;
                options.Lockout.DefaultLockoutTimeSpan = lockoutOptions.BaseLockoutDuration;
                options.SignIn.RequireConfirmedEmail = true;
                // Version3 adds IdentityUserPasskey — required for WebAuthn/passkey support.
                options.Stores.SchemaVersion = IdentitySchemaVersions.Version3;
            })
            .AddEntityFrameworkStores<IdentityLocalDbContext>()
            .AddDefaultTokenProviders();

        // 4. Group store — required by AspNetIdentityProvider. Registered here (not only in the module)
        //    so a host that wires persistence via this extension always has it, independent of module
        //    discovery.
        builder.Services.TryAddScoped<ILocalIdentityGroupStore, IdentityLocalGroupStore>();

        // 5. Accessor exposing the scoped IdentityLocalDbContext as IIdentityDbContextAccessor —
        //    consumed by IGranitRoleOrchestrator to share the Identity transaction with the host
        //    authorization DbContext when both target the same database.
        builder.Services.TryAddScoped<IIdentityDbContextAccessor, IdentityLocalDbContextAccessor>();

        // 5. Query-engine sources for the identity entities — back MapGranitQuery<T> and the analytics
        //    runners over GranitRoleQuery / GranitUserGroupQuery.
        builder.Services.AddScoped<IQueryableSource<GranitRole>, EfGranitRoleQueryableSource>();
        builder.Services.AddScoped<IQueryableSource<GranitUserGroup>, EfGranitUserGroupQueryableSource>();

        return builder;
    }
}
