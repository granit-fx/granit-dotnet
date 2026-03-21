using Granit.Core.Modularity;
using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.Identity.OpenIddict.Internal;
using Granit.OpenIddict.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.Identity.OpenIddict;

/// <summary>
/// Granit module that registers <see cref="AspNetIdentityProvider"/> as the
/// <see cref="IIdentityProvider"/> implementation, replacing the default
/// <c>NullIdentityProvider</c>.
/// </summary>
/// <remarks>
/// <para>
/// Do NOT add <c>Granit.Identity.EntityFrameworkCore</c> alongside this package —
/// it would create a redundant <c>UserCache</c> layer.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitIdentityModule),
    typeof(GranitOpenIddictEntityFrameworkCoreModule))]
public sealed class GranitIdentityOpenIddictModule : GranitModule
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
}
