using System.Diagnostics.CodeAnalysis;
using Granit.IpGeolocation.MaxMind.Internal;
using Granit.IpGeolocation.MaxMind.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.MaxMind.Extensions;

/// <summary>
/// Extension methods for registering the offline MaxMind IP geolocation provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class IpGeolocationMaxMindHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the MaxMind (<c>.mmdb</c>) offline geolocation provider as an <see cref="IIpGeolocationProvider"/>.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="MaxMindIpGeolocationOptions"/> from <c>"IpGeolocation:MaxMind"</c> and validates the
    /// database path at startup. Remember to add the configured <c>ProviderName</c> (default <c>"MaxMind"</c>)
    /// to <c>IpGeolocation:ProviderOrder</c> so the resolver actually uses it.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitIpGeolocationMaxMind(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<MaxMindIpGeolocationOptions>()
            .BindConfiguration(MaxMindIpGeolocationOptions.SectionName)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<MaxMindIpGeolocationOptions>, MaxMindIpGeolocationOptionsValidator>();

        builder.Services.AddSingleton<MaxMindDatabaseProvider>();
        builder.Services.AddSingleton<IIpGeolocationProvider, MaxMindIpGeolocationProvider>();

        return builder;
    }
}
