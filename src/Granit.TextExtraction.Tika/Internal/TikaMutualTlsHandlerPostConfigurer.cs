using Granit.TextExtraction.Tika.Options;
using Microsoft.Extensions.Http;
using Microsoft.Extensions.Options;

namespace Granit.TextExtraction.Tika.Internal;

/// <summary>
/// Startup-time enforcement for <see cref="TikaSidecarOptions.RequireMutualTls"/>. Hooks the
/// post-configure pipeline of <see cref="HttpClientFactoryOptions"/> for the
/// <see cref="TikaSidecarTextExtractor.HttpClientName"/> named client and throws when the host
/// opted in to mTLS but registered no <c>.ConfigurePrimaryHttpMessageHandler(...)</c> /
/// <c>.AddHttpMessageHandler(...)</c> on the named client.
/// </summary>
/// <remarks>
/// <para>
/// Why <c>HttpMessageHandlerBuilderActions</c> counting and not type inspection:
/// the framework default <c>PrimaryHandler</c> differs across platforms (Windows ships
/// <c>HttpClientHandler</c>, Linux ships <c>SocketsHttpHandler</c>) and across major .NET
/// versions, so a type check is brittle. Counting the host-registered builder actions is
/// stable: every <c>ConfigurePrimaryHttpMessageHandler</c> /
/// <c>AddHttpMessageHandler</c> / <c>ConfigureHttpMessageHandlerBuilder</c> extension adds
/// exactly one action to that list, and they all run before
/// <see cref="IPostConfigureOptions{TOptions}.PostConfigure"/> fires.
/// </para>
/// <para>
/// Why throw at <see cref="IPostConfigureOptions{TOptions}.PostConfigure"/> rather than at
/// the first builder action: the throw runs on the very
/// first <see cref="IHttpClientFactory.CreateClient(string)"/> call for the named client —
/// i.e. before any extraction request reaches Tika in plaintext — and the failure surfaces
/// in the host's startup logs rather than buried in a request-time exception filter.
/// </para>
/// </remarks>
internal sealed class TikaMutualTlsHandlerPostConfigurer
    : IPostConfigureOptions<HttpClientFactoryOptions>
{
    private readonly IOptionsMonitor<TikaSidecarOptions> _tikaOptions;

    public TikaMutualTlsHandlerPostConfigurer(IOptionsMonitor<TikaSidecarOptions> tikaOptions)
    {
        ArgumentNullException.ThrowIfNull(tikaOptions);
        _tikaOptions = tikaOptions;
    }

    public void PostConfigure(string? name, HttpClientFactoryOptions options)
    {
        if (!string.Equals(name, TikaSidecarTextExtractor.HttpClientName, StringComparison.Ordinal))
        {
            return;
        }

        ArgumentNullException.ThrowIfNull(options);

        TikaSidecarOptions tika = _tikaOptions.CurrentValue;
        if (!tika.RequireMutualTls)
        {
            return;
        }

        // All host .ConfigurePrimaryHttpMessageHandler(...) / .AddHttpMessageHandler(...) calls
        // have already pushed their actions into this list by the time PostConfigure fires.
        // An empty list means the host wired nothing — the default platform handler would be
        // used with no client cert and no mesh handler. Fail loudly NOW so the misconfiguration
        // is visible in startup logs instead of silently leaking documents in plaintext.
        if (options.HttpMessageHandlerBuilderActions.Count == 0)
        {
            throw new InvalidOperationException(
                $"TikaSidecarOptions.RequireMutualTls is true but no custom message handler " +
                $"is configured for the '{TikaSidecarTextExtractor.HttpClientName}' HttpClient. " +
                "Call .ConfigurePrimaryHttpMessageHandler(...) on the IHttpClientBuilder " +
                "returned by AddTikaSidecarExtractor(...) to attach a client certificate or " +
                "a mesh-aware handler, OR set RequireMutualTls=false in " +
                "appsettings.Development.json (NEVER in production).");
        }
    }
}
