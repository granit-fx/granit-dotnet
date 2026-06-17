using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using OpenIddict.Validation;

namespace Granit.OpenIddict.Server.Handlers;

/// <summary>
/// OpenIddict validation handler that extracts access tokens presented with the
/// <c>Authorization: DPoP &lt;token&gt;</c> scheme (RFC 9449 §7.1).
/// </summary>
/// <remarks>
/// OpenIddict 7.x's built-in <c>ExtractAccessTokenFromAuthorizationHeader</c> only accepts
/// the <c>Bearer</c> scheme. This handler runs immediately after it and picks up
/// DPoP-schemed tokens, enabling the local-server validation pipeline (monolith) to
/// authenticate requests sent by the Granit BFF when <c>UseDPoP</c> is enabled.
/// </remarks>
public sealed class DPoPValidationTokenExtractionHandler
    : IOpenIddictValidationHandler<OpenIddictValidationEvents.ProcessAuthenticationContext>
{
    // OpenIddict internals (from reflection on 7.5.0):
    //   EvaluateValidatedTokens.Order     = -2_147_383_648
    //   ExtractAccessTokenFromAuth.Order  = EvaluateValidatedTokens.Order + 500  = -2_147_383_148
    // This handler runs one slot later as a fallback.
    private const int HandlerOrder = -2_147_383_148 + 1;

    /// <summary>Handler descriptor registered with the OpenIddict validation pipeline.</summary>
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
        = OpenIddictValidationHandlerDescriptor
            .CreateBuilder<OpenIddictValidationEvents.ProcessAuthenticationContext>()
            .UseSingletonHandler<DPoPValidationTokenExtractionHandler>()
            .SetOrder(HandlerOrder)
            .SetType(OpenIddictValidationHandlerType.Custom)
            .Build();

    /// <inheritdoc/>
    public ValueTask HandleAsync(OpenIddictValidationEvents.ProcessAuthenticationContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Skip if the Bearer extraction handler already found a token.
        if (!string.IsNullOrEmpty(context.AccessToken))
        {
            return ValueTask.CompletedTask;
        }

        HttpRequest? request = context.Transaction.GetHttpRequest();
        if (request is null)
        {
            return ValueTask.CompletedTask;
        }

        string? authorization = request.Headers[HeaderNames.Authorization];
        if (!string.IsNullOrEmpty(authorization)
            && authorization.StartsWith("DPoP ", StringComparison.OrdinalIgnoreCase))
        {
            context.AccessToken = authorization["DPoP ".Length..];
        }

        return ValueTask.CompletedTask;
    }
}
