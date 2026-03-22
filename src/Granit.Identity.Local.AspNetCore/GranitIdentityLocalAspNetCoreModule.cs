using Granit.Core.Modularity;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.Local.AspNetCore.Internal;
using Granit.OpenIddict.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.AspNetCore;

/// <summary>
/// Granit module that registers <see cref="AspNetIdentityProvider"/> as the
/// <see cref="IIdentityProvider"/> implementation, replacing the default
/// <c>NullIdentityProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// Do NOT add <c>Granit.Identity.Federated.EntityFrameworkCore</c> alongside this package —
/// it would create a redundant <c>UserCache</c> layer. A warning is logged at
/// startup if both are detected.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitIdentityModule),
    typeof(GranitOpenIddictEntityFrameworkCoreModule))]
public sealed class GranitIdentityLocalAspNetCoreModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddIdentityProvider<AspNetIdentityProvider>();
        context.Services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities,
            AspNetIdentityProviderCapabilities>());
        context.Services.Replace(ServiceDescriptor.Scoped<IUserLookupService,
            AspNetIdentityUserLookupService>());
    }

    /// <inheritdoc/>
    public override void OnApplicationInitialization(ApplicationInitializationContext context)
    {
        // Detect redundant Granit.Identity.Federated.EntityFrameworkCore registration.
        // When OpenIddict is the identity provider, GranitUser is the source of truth —
        // UserCacheEntry and UserCacheSyncMiddleware are unnecessary.
        var userCacheDbContextType = Type.GetType(
            "Granit.Identity.Federated.EntityFrameworkCore.DbContext.IUserCacheDbContext, Granit.Identity.Federated.EntityFrameworkCore",
            throwOnError: false);

        if (userCacheDbContextType is not null)
        {
            ILogger<GranitIdentityLocalAspNetCoreModule> logger = context.ServiceProvider
                .GetRequiredService<ILogger<GranitIdentityLocalAspNetCoreModule>>();

            logger.LogWarning(
                "Granit.Identity.Federated.EntityFrameworkCore is loaded alongside Granit.Identity.Local.AspNetCore. " +
                "UserCacheEntry is redundant when the identity provider stores users locally (GranitUser). " +
                "Remove the Granit.Identity.Federated.EntityFrameworkCore package reference to avoid an unnecessary " +
                "database table and per-request cache sync overhead.");
        }
    }
}
