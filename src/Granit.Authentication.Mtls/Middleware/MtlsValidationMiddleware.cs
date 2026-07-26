using System.Buffers.Text;
using System.Diagnostics;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json;
using Granit.Authentication.Mtls.Diagnostics;
using Granit.Authentication.Mtls.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.Mtls.Middleware;

/// <summary>
/// Enforces mutual-TLS certificate-bound access tokens (RFC 8705 §3): when an authenticated request
/// carries a <c>cnf.x5t#S256</c> confirmation claim, the SHA-256 thumbprint of the presented client
/// certificate must equal it, otherwise the request is rejected with <c>401</c>.
/// </summary>
/// <remarks>
/// Runs after <c>UseAuthentication()</c>. Unbound (plain Bearer) tokens pass through unless
/// <see cref="MtlsValidationOptions.RequireCertificateBinding"/> is set. The client certificate is
/// read from <see cref="ConnectionInfo.ClientCertificate"/> — populated by Kestrel on a direct mTLS
/// connection or by the certificate-forwarding middleware from a trusted header behind an ingress.
/// </remarks>
internal sealed partial class MtlsValidationMiddleware(
    RequestDelegate next,
    MtlsValidationMetrics metrics,
    IOptions<MtlsValidationOptions> options,
    ILogger<MtlsValidationMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Certificate binding is a property of an authenticated token; anonymous requests are left
        // to the authentication/authorization pipeline.
        if (context.User.Identity?.IsAuthenticated != true)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        string? boundThumbprint = ExtractBoundThumbprint(context.User);

        if (boundThumbprint is null)
        {
            if (options.Value.RequireCertificateBinding)
            {
                Reject(context, "certificate_binding_required");
                return;
            }

            await next(context).ConfigureAwait(false);
            return;
        }

        using Activity? activity = MtlsValidationActivitySource.Source.StartActivity(
            MtlsValidationActivitySource.Validate);

        X509Certificate2? certificate = context.Connection.ClientCertificate;
        if (certificate is null)
        {
            Reject(context, "certificate_missing");
            return;
        }

        string presentedThumbprint = Base64Url.EncodeToString(SHA256.HashData(certificate.RawData));
        if (!string.Equals(presentedThumbprint, boundThumbprint, StringComparison.Ordinal))
        {
            Reject(context, "certificate_mismatch");
            return;
        }

        metrics.RecordSuccess(context.User.FindFirst("tenant_id")?.Value);
        await next(context).ConfigureAwait(false);
    }

    private void Reject(HttpContext context, string reason)
    {
        LogRejected(logger, reason);
        metrics.RecordFailure(reason, context.User.FindFirst("tenant_id")?.Value);

        // Short-circuit: the pipeline stops here (next is not invoked); the server flushes the response.
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.Headers.WWWAuthenticate =
            "Bearer error=\"invalid_token\", error_description=\"certificate-bound token validation failed\"";
    }

    /// <summary>Reads <c>cnf.x5t#S256</c> from the principal, or <see langword="null"/> if the token is unbound.</summary>
    private static string? ExtractBoundThumbprint(ClaimsPrincipal user)
    {
        Claim? cnf = user.FindFirst("cnf");
        if (cnf is null || string.IsNullOrEmpty(cnf.Value))
        {
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(cnf.Value);
            return document.RootElement.TryGetProperty("x5t#S256", out JsonElement value)
                ? value.GetString()
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "mTLS validation: rejected certificate-bound token — {Reason}.")]
    private static partial void LogRejected(ILogger logger, string reason);
}
