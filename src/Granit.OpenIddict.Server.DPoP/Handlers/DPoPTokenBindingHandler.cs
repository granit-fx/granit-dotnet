using System.Security.Claims;
using System.Text.Json;
using Granit.Authentication.DPoP.Validation;
using Granit.OpenIddict.Options;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Server;
using static OpenIddict.Server.OpenIddictServerEvents;

namespace Granit.OpenIddict.Server.DPoP.Handlers;

/// <summary>
/// OpenIddict server handler that validates the <c>DPoP</c> proof JWT presented at the
/// token endpoint and stamps the resulting JWK Thumbprint as the <c>cnf.jkt</c>
/// confirmation claim on the issued access token (RFC 9449 §6).
/// </summary>
/// <remarks>
/// <para>
/// Without this binding, the resource-side <c>DPoPValidationMiddleware</c> has no
/// thumbprint to verify against — DPoP proof validation devolves to a decorative
/// signature check that does not constrain token holdership. With it, an attacker
/// who exfiltrates the access token cannot replay it without also possessing the
/// DPoP private key.
/// </para>
/// <para>
/// In FAPI 2.0 mode (<see cref="GranitOpenIddictOptions.EnableFapi2Profile"/>) a
/// missing DPoP header at <c>/connect/token</c> rejects the request. Outside FAPI
/// 2.0 the handler is opportunistic: a missing header leaves the token unbound
/// (preserving Bearer flows for clients that do not opt in).
/// </para>
/// </remarks>
public sealed partial class DPoPTokenBindingHandler(
    IDPoPProofValidator validator,
    IOptions<GranitOpenIddictOptions> options,
    ILogger<DPoPTokenBindingHandler> logger)
    : IOpenIddictServerHandler<ProcessSignInContext>
{
    /// <summary>
    /// Descriptor registered with OpenIddict's server pipeline. Runs after
    /// <c>ClientSideAuthorizationHandler</c> so the principal is finalized
    /// before we attach the confirmation claim.
    /// </summary>
    public static OpenIddictServerHandlerDescriptor Descriptor { get; }
        = OpenIddictServerHandlerDescriptor.CreateBuilder<ProcessSignInContext>()
            .UseScopedHandler<DPoPTokenBindingHandler>()
            .SetOrder(150_000)
            .SetType(OpenIddictServerHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public async ValueTask HandleAsync(ProcessSignInContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // RFC 9449 §5: DPoP proofs are presented only at the token endpoint. This handler
        // runs on every ProcessSignInContext (authorization, device, and end-user-verification
        // sign-ins all flow through it); without this gate an interactive /connect/authorize
        // sign-in — which carries no DPoP header — would be rejected under FAPI 2.0, breaking
        // the authorization-code and device flows.
        if (context.EndpointType is not OpenIddictServerEndpointType.Token)
        {
            return;
        }

        HttpRequest? request = context.Transaction.GetHttpRequest();
        if (request is null)
        {
            return;
        }

        bool fapi2 = options.Value.EnableFapi2Profile;
        bool hasHeader = request.Headers.TryGetValue("DPoP", out Microsoft.Extensions.Primitives.StringValues headerValues);
        string proofJwt = hasHeader ? headerValues.ToString() : string.Empty;

        if (string.IsNullOrEmpty(proofJwt))
        {
            if (fapi2)
            {
                LogDPoPRequiredFapi2(logger);
                context.Reject(
                    error: OpenIddictConstants.Errors.InvalidRequest,
                    description: "DPoP proof header is required in FAPI 2.0 profile.");
            }

            return;
        }

        string requestUri = $"{request.Scheme}://{request.Host}{request.Path}";
        DPoPValidationResult result = await validator
            .ValidateAsync(proofJwt, request.Method, requestUri, context.CancellationToken)
            .ConfigureAwait(false);

        if (!result.IsValid)
        {
            LogProofRejected(logger, result.Error!);
            context.Reject(
                error: OpenIddictConstants.Errors.InvalidRequest,
                description: $"DPoP proof validation failed: {result.Error}");
            return;
        }

        ClaimsPrincipal? principal = context.Principal;
        if (principal is null)
        {
            return;
        }

        string cnfJson = JsonSerializer.Serialize(new { jkt = result.JwkThumbprint });
        Claim cnf = new(OpenIddictConstants.Claims.Confirmation, cnfJson, "JSON");
        cnf.SetDestinations(OpenIddictConstants.Destinations.AccessToken);
        principal.Identities.First().AddClaim(cnf);
    }

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DPoP token binding: FAPI 2.0 enabled but DPoP header is missing on /connect/token — rejecting.")]
    private static partial void LogDPoPRequiredFapi2(ILogger logger);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "DPoP token binding: proof validation failed — {Error}")]
    private static partial void LogProofRejected(ILogger logger, string error);
}
