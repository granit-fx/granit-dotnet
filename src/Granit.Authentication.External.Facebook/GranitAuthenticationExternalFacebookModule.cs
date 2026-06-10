using Granit.Authentication.External.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.Facebook;

/// <summary>
/// Registers a Facebook authentication handler for each <c>Authentication:External:Providers</c>
/// entry of type <c>Facebook</c>.
/// </summary>
[DependsOn(typeof(GranitAuthenticationExternalModule))]
public sealed class GranitAuthenticationExternalFacebookModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddExternalProviderSchemes(
            context.Configuration,
            "Facebook",
            static (builder, provider) => builder.AddFacebook(
                provider.SchemeName, options => options.ApplyExternalProvider(provider)));
}
