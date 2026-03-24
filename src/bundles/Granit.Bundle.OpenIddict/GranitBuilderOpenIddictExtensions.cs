using Granit.Identity.Local.AspNetIdentity;
using Granit.Modularity;
using Granit.OpenIddict;
using Granit.OpenIddict.BackgroundJobs;
using Granit.OpenIddict.Endpoints;
using Granit.OpenIddict.EntityFrameworkCore;

namespace Granit.Bundle.OpenIddict;

/// <summary>
/// Extension methods on <see cref="GranitBuilder"/> for adding the OpenIddict bundle.
/// </summary>
public static class GranitBuilderOpenIddictExtensions
{
    /// <summary>
    /// Adds all Granit.OpenIddict modules: OIDC server, Identity bridge, endpoints,
    /// external logins, background jobs, and passkeys.
    /// </summary>
    public static GranitBuilder AddOpenIddict(this GranitBuilder builder)
    {
        builder.AddModule<GranitOpenIddictModule>();
        builder.AddModule<GranitOpenIddictEntityFrameworkCoreModule>();
        builder.AddModule<GranitIdentityLocalAspNetIdentityModule>();
        builder.AddModule<GranitOpenIddictEndpointsModule>();
        builder.AddModule<GranitOpenIddictBackgroundJobsModule>();
        return builder;
    }
}
