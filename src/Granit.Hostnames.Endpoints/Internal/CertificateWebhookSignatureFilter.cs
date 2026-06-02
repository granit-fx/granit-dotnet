using System.Security.Cryptography;
using System.Text;
using Granit.Hostnames.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace Granit.Hostnames.Endpoints.Internal;

/// <summary>
/// Endpoint filter that validates the HMAC-SHA-256 signature on the
/// <c>POST /{id}/certificate-status</c> webhook before the handler runs.
/// Skipped when <see cref="HostnamesOptions.CertificateWebhookSecret"/> is not configured
/// (useful for local development / integration tests).
/// </summary>
internal sealed class CertificateWebhookSignatureFilter(IOptions<HostnamesOptions> options) : IEndpointFilter
{
    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        string? secret = options.Value.CertificateWebhookSecret;
        if (string.IsNullOrEmpty(secret))
        {
            return await next(context);
        }

        HttpContext httpContext = context.HttpContext;
        string headerName = options.Value.CertificateWebhookSignatureHeader;

        if (!httpContext.Request.Headers.TryGetValue(headerName, out StringValues sigHeader)
            || StringValues.IsNullOrEmpty(sigHeader))
        {
            return TypedResults.Problem(
                detail: $"Missing webhook signature header '{headerName}'.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        // Buffer the body so model binding can still read it after we verify the signature.
        httpContext.Request.EnableBuffering();
        string body = await new StreamReader(httpContext.Request.Body, Encoding.UTF8, leaveOpen: true)
            .ReadToEndAsync(httpContext.RequestAborted)
            .ConfigureAwait(false);
        httpContext.Request.Body.Position = 0;

        byte[] secretBytes = Encoding.UTF8.GetBytes(secret);
        byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
        byte[] expectedHash = HMACSHA256.HashData(secretBytes, bodyBytes);
        string expectedSig = $"sha256={Convert.ToHexString(expectedHash).ToLowerInvariant()}";

        string receivedSig = sigHeader.ToString();

        // Constant-time comparison prevents timing-based signature oracle attacks.
        // The expected length is always fixed (71 chars: "sha256=" + 64 hex digits).
        bool valid = CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expectedSig),
            Encoding.ASCII.GetBytes(receivedSig));

        if (!valid)
        {
            return TypedResults.Problem(
                detail: "Invalid webhook signature.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        return await next(context);
    }
}
