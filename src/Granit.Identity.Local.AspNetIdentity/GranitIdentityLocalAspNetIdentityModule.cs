using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.Local;
using Granit.Identity.Local.AspNetIdentity.Internal;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Services;
using Granit.Modularity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;

namespace Granit.Identity.Local.AspNetIdentity;

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
    typeof(GranitIdentityLocalModule),
    typeof(GranitIdentityModule))]
public sealed partial class GranitIdentityLocalAspNetIdentityModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddIdentityProvider<AspNetIdentityProvider>();
        context.Services.Replace(ServiceDescriptor.Scoped<IIdentityProviderCapabilities,
            AspNetIdentityProviderCapabilities>());
        context.Services.Replace(ServiceDescriptor.Scoped<IUserLookupService,
            AspNetIdentityUserLookupService>());

        // Replace default UserManager with GranitUserManager (exponential backoff lockout)
        context.Services.Replace(ServiceDescriptor.Scoped<UserManager<GranitUser>, GranitUserManager>());

        // ASP.NET Core Identity service implementations (depend on UserManager<GranitUser>)
        context.Services.TryAddScoped<ITotpService, TotpService>();
        context.Services.TryAddScoped<ITwoFactorService, AspNetTwoFactorService>();
        context.Services.TryAddScoped<IPasskeyService, AspNetPasskeyService>();
        context.Services.TryAddScoped<IPasswordResetService, AspNetPasswordResetService>();
        context.Services.TryAddScoped<IEmailChangeService, AspNetEmailChangeService>();
        context.Services.TryAddScoped<IEmailConfirmationService, AspNetEmailConfirmationService>();
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
            ILogger<GranitIdentityLocalAspNetIdentityModule> logger = context.ServiceProvider
                .GetRequiredService<ILogger<GranitIdentityLocalAspNetIdentityModule>>();

            Log.RedundantFederatedModuleDetected(logger);
        }
    }

    private static partial class Log
    {
        [LoggerMessage(Level = LogLevel.Warning,
            Message = "Granit.Identity.Federated.EntityFrameworkCore is loaded alongside "
                + "Granit.Identity.Local.AspNetIdentity. UserCacheEntry is redundant when the "
                + "identity provider stores users locally (GranitUser). Remove the "
                + "Granit.Identity.Federated.EntityFrameworkCore package reference to avoid an "
                + "unnecessary database table and per-request cache sync overhead.")]
        public static partial void RedundantFederatedModuleDetected(ILogger logger);
    }
}
