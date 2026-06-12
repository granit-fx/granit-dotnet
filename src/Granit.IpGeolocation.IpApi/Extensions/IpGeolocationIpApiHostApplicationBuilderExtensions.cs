using System.Diagnostics.CodeAnalysis;
using Granit.IpGeolocation.IpApi.Internal;
using Granit.IpGeolocation.IpApi.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Granit.IpGeolocation.IpApi.Extensions;

/// <summary>
/// Extension methods for registering the opt-in ipinfo.io IP geolocation provider.
/// </summary>
// DI wiring only — no logic to unit test.
[ExcludeFromCodeCoverage]
public static class IpGeolocationIpApiHostApplicationBuilderExtensions
{
    /// <summary>
    /// Adds the ipinfo.io geolocation provider as an <see cref="IIpGeolocationProvider"/>.
    /// </summary>
    /// <remarks>
    /// Binds <see cref="IpApiIpGeolocationOptions"/> from <c>"IpGeolocation:IpApi"</c>. <strong>GDPR:</strong>
    /// this transmits client IPs to a third-party processor — only call this when that data flow is approved,
    /// and add the configured <c>ProviderName</c> (default <c>"IpApi"</c>) to <c>IpGeolocation:ProviderOrder</c>.
    /// The primary handler disables auto-redirects to close the redirect-based SSRF path.
    /// </remarks>
    /// <param name="builder">The host application builder.</param>
    /// <returns>The builder for chaining.</returns>
    public static IHostApplicationBuilder AddGranitIpGeolocationIpApi(this IHostApplicationBuilder builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.Services
            .AddOptions<IpApiIpGeolocationOptions>()
            .BindConfiguration(IpApiIpGeolocationOptions.SectionName)
            .ValidateOnStart();

        builder.Services
            .AddSingleton<IValidateOptions<IpApiIpGeolocationOptions>, IpApiIpGeolocationOptionsValidator>();

        builder.Services.AddTransient<IpAddressTelemetryRedactionHandler>();

        builder.Services
            .AddHttpClient(IpApiIpGeolocationProvider.HttpClientName, static (sp, client) =>
            {
                IpApiIpGeolocationOptions options =
                    sp.GetRequiredService<IOptions<IpApiIpGeolocationOptions>>().Value;
                client.BaseAddress = options.BaseAddress;
                client.Timeout = options.Timeout;
            })
            .ConfigurePrimaryHttpMessageHandler(static () => new SocketsHttpHandler
            {
                AllowAutoRedirect = false,
            })
            .AddHttpMessageHandler<IpAddressTelemetryRedactionHandler>();

        builder.Services.AddSingleton<IIpGeolocationProvider, IpApiIpGeolocationProvider>();

        return builder;
    }
}
