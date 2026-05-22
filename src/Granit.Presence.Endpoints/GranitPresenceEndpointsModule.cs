using Granit.Authorization;
using Granit.Localization.Extensions;
using Granit.Modularity;
using Granit.Presence.Endpoints.Internal;
using Granit.Validation;

namespace Granit.Presence.Endpoints;

/// <summary>
/// Granit module exposing the presence HTTP endpoints, permissions, validators, and localized
/// error messages.
/// </summary>
[DependsOn(
    typeof(GranitAuthorizationModule),
    typeof(GranitPresenceModule),
    typeof(GranitValidationModule))]
public sealed class GranitPresenceEndpointsModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        context.Services.AddLocalizationResource<PresenceEndpointsLocalizationResource>();
    }
}
