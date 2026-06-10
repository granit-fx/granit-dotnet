using Granit.Authentication.External.Extensions;
using Granit.Modularity;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Authentication.External.Microsoft;

/// <summary>
/// Registers a Microsoft account authentication handler for each
/// <c>Authentication:External:Providers</c> entry of type <c>Microsoft</c>.
/// </summary>
[DependsOn(typeof(GranitAuthenticationExternalModule))]
public sealed class GranitAuthenticationExternalMicrosoftModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddExternalProviderSchemes(
            context.Builder!.Configuration,
            "Microsoft",
            static (builder, provider) => builder.AddMicrosoftAccount(
                provider.SchemeName, options => options.ApplyExternalProvider(provider)));
}
