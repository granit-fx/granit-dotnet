using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.Net.Http.Headers;
using OpenIddict.Validation;
using OpenIddict.Validation.AspNetCore;

namespace Granit.OpenIddict.Server.Handlers;

/// <summary>
/// OpenIddict validation handler that extracts access tokens presented with the
/// <c>Authorization: DPoP &lt;token&gt;</c> scheme (RFC 9449 §7.1).
/// </summary>
/// <remarks>
/// OpenIddict has no DPoP support of its own — its built-in
/// <c>ExtractAccessTokenFromAuthorizationHeader</c> only handles the <c>Bearer</c> scheme.
/// DPoP is implemented entirely by Granit on top of OpenIddict's pipeline, so extracting
/// DPoP-schemed tokens in the validation stack is an intrinsic, permanent part of that
/// integration — not a workaround for an upstream defect. This handler runs as a fallback
/// after the Bearer extractor and populates the access token for any DPoP client: a BFF, or
/// a no-BFF consumer (SPA, mobile, native) where mTLS token binding is impractical.
/// </remarks>
public sealed class DPoPValidationTokenExtractionHandler
    : IOpenIddictValidationHandler<OpenIddictValidationEvents.ProcessAuthenticationContext>
{
    // Run one slot after the built-in Bearer extractor so this acts as a fallback.
    // Reference its public Descriptor.Order rather than a hardcoded constant: the
    // order then tracks the built-in automatically across OpenIddict version bumps.
    private static readonly int HandlerOrder = checked((int)(
        OpenIddictValidationAspNetCoreHandlers.ExtractAccessTokenFromAuthorizationHeader
            .Descriptor.Order + 1));

    /// <summary>Handler descriptor registered with the OpenIddict validation pipeline.</summary>
    public static OpenIddictValidationHandlerDescriptor Descriptor { get; }
        = OpenIddictValidationHandlerDescriptor
            .CreateBuilder<OpenIddictValidationEvents.ProcessAuthenticationContext>()
            .AddFilter<OpenIddictValidationAspNetCoreHandlerFilters.RequireHttpRequest>()
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
