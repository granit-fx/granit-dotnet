using Granit.Authentication.External.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.Google;

/// <summary>
/// Registers a Google authentication handler for each <c>Authentication:External:Providers</c>
/// entry of type <c>Google</c>.
/// </summary>
[DependsOn(typeof(GranitAuthenticationExternalModule))]
public sealed class GranitAuthenticationExternalGoogleModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddExternalProviderSchemes(
            context.Configuration,
            "Google",
            static (builder, provider) => builder.AddGoogle(
                provider.SchemeName, options => options.ApplyExternalProvider(provider)));
}
