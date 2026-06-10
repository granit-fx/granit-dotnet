using Granit.Authentication.External.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.GitHub;

/// <summary>
/// Registers a GitHub authentication handler for each <c>Authentication:External:Providers</c>
/// entry of type <c>GitHub</c>.
/// </summary>
[DependsOn(typeof(GranitAuthenticationExternalModule))]
public sealed class GranitAuthenticationExternalGitHubModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddExternalProviderSchemes(
            context.Configuration,
            "GitHub",
            static (builder, provider) => builder.AddGitHub(
                provider.SchemeName, options => options.ApplyExternalProvider(provider)));
}
