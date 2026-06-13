using Granit.Identity;
using Granit.Identity.Extensions;
using Granit.OpenIddict.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.OpenIddict.Extensions;

/// <summary>
/// Registers the OpenIddict-backed session/device provider for the canonical session API.
/// </summary>
public static class OpenIddictUserSessionServiceCollectionExtensions
{
    /// <summary>
    /// Registers <c>OpenIddictUserSessionProvider</c> as the active <see cref="IUserSessionProvider"/> and
    /// <see cref="IUserDeviceProvider"/> at <see cref="UserSessionProviderPrecedence.OpenIddict"/>, replacing
    /// the no-op defaults. Called by <c>AddGranitOpenIddict</c> so wiring the authority also wires its
    /// providers — they are no longer registered by <c>GranitOpenIddictModule</c>, which a host can omit
    /// from its module graph while still calling <c>AddGranitOpenIddict</c>.
    /// </summary>
    public static IServiceCollection AddOpenIddictUserSessionProvider(this IServiceCollection services) =>
        services.SetUserSessionProvider<OpenIddictUserSessionProvider>(UserSessionProviderPrecedence.OpenIddict);
}
