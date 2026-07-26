using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Granit.OpenIddict.Options;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.Mtls.Handlers;

/// <summary>
/// OpenIddict server handler that binds the issued access token to the client's mutual-TLS
/// certificate, stamping the certificate SHA-256 thumbprint as the <c>cnf.x5t#S256</c>
/// confirmation claim on the access token (RFC 8705 §3).
/// </summary>
/// <remarks>
/// <para>
/// The client certificate is read from <see cref="ConnectionInfo.ClientCertificate"/> — populated
/// by Kestrel on a direct mTLS connection, or by the certificate-forwarding middleware from a
/// trusted header when TLS is terminated at an ingress. Without this binding, a leaked access token
/// can be replayed by any caller; with it, replay also requires the client's private key.
/// </para>
/// <para>
/// In FAPI 2.0 mode (<see cref="GranitOpenIddictOptions.EnableFapi2Profile"/>) a missing client
/// certificate at <c>/connect/token</c> rejects the request. Outside FAPI 2.0 the handler is
/// opportunistic: a missing certificate leaves the token unbound (preserving Bearer flows).
/// </para>
/// </remarks>
public sealed partial class MtlsTokenBindingHandler(
    IOptions<GranitOpenIddictOptions> options,
    ILogger<MtlsTokenBindingHandler> logger)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    /// <summary>
    /// Descriptor registered with OpenIddict's server pipeline. Runs after the principal is
    /// finalized so the confirmation claim binds the token that is actually issued.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<MtlsTokenBindingHandler>()
            .SetOrder(150_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public ValueTask HandleAsync(ProcessSignInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // RFC 8705: certificate-bound tokens apply at the token endpoint. This handler runs on
        // every ProcessSignInContext; without this gate an interactive /connect/authorize sign-in —
        // which carries no client certificate — would be rejected under FAPI 2.0.
        if (context.EndpointType is not OpenIddictServerEndpointType.Token)
        {
            return ValueTask.CompletedTask;
        }

        HttpRequest? request = context.Transaction.GetHttpRequest();

        // Framework-owned: do not dispose — Connection.ClientCertificate may be reused per request.
        X509Certificate2? certificate = request?.HttpContext.Connection.ClientCertificate;

        if (certificate is null)
        {
            if (options.Value.EnableFapi2Profile)
            {
                LogCertificateRequiredFapi2(logger);
                context.Reject(
                    error: OpenIddictConstants.Errors.InvalidRequest,
                    description: "A client certificate is required in FAPI 2.0 profile (mTLS sender-constraining).");
            }

            return ValueTask.CompletedTask;
        }

        ClaimsPrincipal? principal = context.Principal;
        if (principal is null)
        {
            return ValueTask.CompletedTask;
        }

        string thumbprint = ComputeCertificateThumbprint(certificate);

        // Replace, don't append: on refresh_token grants the principal rebuilt from the refresh
        // token can already carry a cnf claim — appending would emit duplicate Confirmation claims.
        ClaimsIdentity identity = principal.Identities.First();
        foreach (Claim existing in identity.FindAll(OpenIddictConstants.Claims.Confirmation).ToList())
        {
            identity.RemoveClaim(existing);
        }

        string cnfJson = JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x5t#S256"] = thumbprint,
        });
        Claim cnf = new(OpenIddictConstants.Claims.Confirmation, cnfJson, "JSON");
        cnf.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        identity.AddClaim(cnf);

        LogCertificateBound(logger);
        return ValueTask.CompletedTask;
    }

    // RFC 8705 §3.1: x5t#S256 = base64url(SHA-256(DER-encoded certificate)).
    private static string ComputeCertificateThumbprint(X509Certificate2 certificate) =>
        Base64Url.EncodeToString(SHA256.HashData(certificate.RawData));

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "mTLS token binding: FAPI 2.0 enabled but no client certificate on /connect/token — rejecting.")]
    private static partial void LogCertificateRequiredFapi2(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Debug,
        Message = "mTLS token binding: access token bound to the client certificate (cnf.x5t#S256).")]
    private static partial void LogCertificateBound(ILogger logger);
}
