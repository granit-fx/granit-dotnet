using System.Net;
using Granit.Bff.Options;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Granit.Bff.Internal;

/// <summary>
/// HTTP message handler that routes requests through the ASP.NET Core middleware
/// pipeline in-memory when the BFF authority is hosted in the same process.
/// Prevents TCP loopback deadlocks in self-hosted (BFF + OpenIddict) scenarios.
/// </summary>
/// <remarks>
/// <para>
/// When the configured <see cref="GranitBffOptions.Authority"/> matches one of the
/// addresses reported by <see cref="IServer"/>, the handler builds an
/// <see cref="HttpContext"/> from the outgoing <see cref="HttpRequestMessage"/> and
/// invokes the request pipeline directly (similar to <c>TestServer.CreateHandler()</c>),
/// bypassing TCP entirely.
/// </para>
/// <para>
/// When the authority does not match (separate OIDC server), the handler delegates
/// to the inner <see cref="HttpMessageHandler"/> which performs a normal network call.
/// </para>
/// </remarks>
internal sealed partial class InternalLoopbackHandler(
    BffLoopbackPipelineCapture pipelineCapture,
    IServer server,
    IOptions<GranitBffOptions> bffOptions,
    IServiceScopeFactory scopeFactory,
    ILogger<InternalLoopbackHandler> logger) : DelegatingHandler
{
    private bool? _isLoopback;

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (!ShouldShortCircuit(request))
        {
            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }

        LogLoopbackRequest(logger, request.RequestUri!);
        return await InvokeInMemoryAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private bool ShouldShortCircuit(HttpRequestMessage request)
    {
        if (pipelineCapture.Pipeline is null || request.RequestUri is null)
        {
            return false;
        }

        if (!IsAuthorityLocal())
        {
            return false;
        }

        // Verify this specific request targets the authority (safety net — all BFF
        // calls go to the authority, but guard against unexpected use of the client).
        Uri authority = bffOptions.Value.Authority;
        return string.Equals(request.RequestUri.Host, authority.Host, StringComparison.OrdinalIgnoreCase)
            && request.RequestUri.Port == authority.Port;
    }

    /// <summary>
    /// Determines whether the configured BFF authority is served by this process
    /// by comparing it against <see cref="IServerAddressesFeature.Addresses"/>.
    /// The result is cached after the first successful evaluation.
    /// </summary>
    private bool IsAuthorityLocal()
    {
        if (_isLoopback.HasValue)
        {
            return _isLoopback.Value;
        }

        IServerAddressesFeature? feature = server.Features.Get<IServerAddressesFeature>();
        if (feature is null)
        {
            return false;
        }

        ICollection<string> addresses = feature.Addresses;
        if (addresses.Count == 0)
        {
            return false; // Server hasn't bound yet — retry on next request
        }

        Uri authority = bffOptions.Value.Authority;

        foreach (string address in addresses)
        {
            if (!TryParseServerAddress(address, out string scheme, out string host, out int port))
            {
                continue;
            }

            if (!string.Equals(scheme, authority.Scheme, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (port != authority.Port)
            {
                continue;
            }

            if (IsWildcardHost(host)
                || string.Equals(host, authority.Host, StringComparison.OrdinalIgnoreCase))
            {
                _isLoopback = true;
                LogLoopbackActive(logger, authority);
                return true;
            }
        }

        _isLoopback = false;
        return false;
    }

    /// <summary>
    /// Parses a Kestrel server address (e.g. <c>https://+:5001</c>, <c>http://[::]:80</c>).
    /// <see cref="Uri.TryCreate(string, UriKind, out Uri?)"/> rejects wildcard hosts
    /// (<c>+</c>, <c>*</c>) that Kestrel accepts, so we parse manually.
    /// </summary>
    private static bool TryParseServerAddress(
        string address, out string scheme, out string host, out int port)
    {
        scheme = host = "";
        port = 0;

        int schemeEnd = address.IndexOf("://", StringComparison.Ordinal);
        if (schemeEnd < 0)
        {
            return false;
        }

        scheme = address[..schemeEnd];
        string remaining = address[(schemeEnd + 3)..];
        int defaultPort = string.Equals(scheme, "https", StringComparison.OrdinalIgnoreCase) ? 443 : 80;

        // IPv6: [::]:port
        if (remaining.StartsWith('['))
        {
            int bracketEnd = remaining.IndexOf(']');
            if (bracketEnd < 0)
            {
                return false;
            }

            host = remaining[1..bracketEnd];
            string afterBracket = remaining[(bracketEnd + 1)..];
            port = afterBracket.StartsWith(':') && int.TryParse(afterBracket[1..], out int ipv6Port)
                ? ipv6Port
                : defaultPort;
            return true;
        }

        // host:port or host
        int colonIndex = remaining.LastIndexOf(':');
        if (colonIndex >= 0 && int.TryParse(remaining[(colonIndex + 1)..], out int parsedPort))
        {
            host = remaining[..colonIndex];
            port = parsedPort;
            return true;
        }

        host = remaining;
        port = defaultPort;
        return true;
    }

    private static bool IsWildcardHost(string host) =>
        host is "+" or "*" or "0.0.0.0" or "::" or "";

    /// <summary>
    /// Invokes the ASP.NET Core middleware pipeline in-memory by building
    /// an <see cref="HttpContext"/> from the outgoing request and converting
    /// the response back to an <see cref="HttpResponseMessage"/>.
    /// </summary>
    private async Task<HttpResponseMessage> InvokeInMemoryAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        await using AsyncServiceScope scope = scopeFactory.CreateAsyncScope();

        DefaultHttpContext context = new()
        {
            RequestServices = scope.ServiceProvider,
            RequestAborted = cancellationToken,
        };

        await MapRequestAsync(request, context.Request, cancellationToken).ConfigureAwait(false);

        MemoryStream responseBody = new();
        context.Response.Body = responseBody;

        await pipelineCapture.Pipeline!(context).ConfigureAwait(false);

        return BuildResponse(context.Response, request);
    }

    private static async Task MapRequestAsync(
        HttpRequestMessage source, HttpRequest target, CancellationToken ct)
    {
        target.Method = source.Method.Method;
        target.Protocol = "HTTP/1.1";
        target.Scheme = source.RequestUri!.Scheme;
        target.Host = source.RequestUri.IsDefaultPort
            ? new HostString(source.RequestUri.Host)
            : new HostString(source.RequestUri.Host, source.RequestUri.Port);
        target.Path = source.RequestUri.AbsolutePath;
        target.QueryString = new QueryString(source.RequestUri.Query);

        foreach (KeyValuePair<string, IEnumerable<string>> header in source.Headers)
        {
            target.Headers.Append(header.Key, new StringValues([.. header.Value]));
        }

        if (source.Content is null)
        {
            return;
        }

        foreach (KeyValuePair<string, IEnumerable<string>> header in source.Content.Headers)
        {
            target.Headers.Append(header.Key, new StringValues([.. header.Value]));
        }

        MemoryStream body = new();
        await source.Content.CopyToAsync(body, ct).ConfigureAwait(false);
        body.Position = 0;
        target.Body = body;

        if (source.Content.Headers.ContentType is not null)
        {
            target.ContentType = source.Content.Headers.ContentType.ToString();
        }

        if (source.Content.Headers.ContentLength.HasValue)
        {
            target.ContentLength = source.Content.Headers.ContentLength;
        }
    }

    private static HttpResponseMessage BuildResponse(HttpResponse source, HttpRequestMessage request)
    {
        source.Body.Position = 0;

        HttpResponseMessage response = new((HttpStatusCode)source.StatusCode)
        {
            Content = new StreamContent(source.Body),
            RequestMessage = request,
        };

        foreach (KeyValuePair<string, StringValues> header in source.Headers)
        {
            if (!response.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value!))
            {
                response.Content.Headers.TryAddWithoutValidation(header.Key, (IEnumerable<string>)header.Value!);
            }
        }

        return response;
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "BFF loopback — routing {RequestUri} through in-memory pipeline")]
    private static partial void LogLoopbackRequest(ILogger logger, Uri requestUri);

    [LoggerMessage(Level = LogLevel.Information, Message = "BFF self-loopback active — authority {Authority} matches server address, HTTP calls bypass TCP")]
    private static partial void LogLoopbackActive(ILogger logger, Uri authority);
}
