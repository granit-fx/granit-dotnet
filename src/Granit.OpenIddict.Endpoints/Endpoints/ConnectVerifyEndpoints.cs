using Granit.OpenIddict.Endpoints.Options;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using OpenIddict.Abstractions;

namespace Granit.OpenIddict.Endpoints.Endpoints;

/// <summary>
/// Device authorization verification endpoint (<c>GET/POST /connect/verify</c>).
/// Handles the end-user verification step of the OAuth 2.0 Device Authorization Grant (RFC 8628).
/// </summary>
/// <remarks>
/// <para>
/// When a device requests a token via <c>/connect/device</c>, it receives a
/// <c>user_code</c> and a <c>verification_uri</c>. The user navigates to
/// <c>/connect/verify</c>, enters the code, and authorizes the device.
/// </para>
/// <para>
/// By default this endpoint redirects to a configurable verification page
/// (<see cref="OpenIddictServerEndpointsOptions.DeviceVerificationPath"/>)
/// where the host application can render the code-entry UI.
/// </para>
/// </remarks>
#pragma warning disable GRAPI001 // OpenIddict requires Results.SignIn/Forbid/Challenge with explicit auth scheme
#pragma warning disable GRAPI003 // Private handler methods — not exposed as endpoint parameters
internal static partial class ConnectVerifyEndpoints
{
    internal static IEndpointRouteBuilder MapConnectVerifyEndpoints(
        this IEndpointRouteBuilder endpoints,
        OpenIddictServerEndpointsOptions options)
    {
        endpoints.MapGet("/connect/verify",
                (Delegate)((HttpContext context) => HandleVerifyAsync(context, options)))
            .ExcludeFromDescription();

        endpoints.MapPost("/connect/verify",
                (Delegate)((HttpContext context) => HandleVerifyAsync(context, options)))
            .ExcludeFromDescription();

        return endpoints;
    }

    private static async Task<IResult> HandleVerifyAsync(
        HttpContext context,
        OpenIddictServerEndpointsOptions options)
    {
        ILogger logger = context.RequestServices
            .GetRequiredService<ILoggerFactory>()
            .CreateLogger("Granit.OpenIddict.Endpoints.ConnectVerifyEndpoints");

        OpenIddictRequest? request = context.GetOpenIddictServerRequest();

        // No user_code present yet — redirect to the host's device verification page.
        if (request is null || string.IsNullOrEmpty((string?)request["user_code"]))
        {
            string path = options.DeviceVerificationPath;
            LogDeviceVerificationRedirect(logger, path);
            return Results.Redirect(path);
        }

        // Redirect with the user_code so the verification page can pre-fill the input.
        string userCode = (string?)request["user_code"] ?? string.Empty;
        string redirectUrl = $"{options.DeviceVerificationPath}?user_code={Uri.EscapeDataString(userCode)}";
        LogDeviceVerificationRedirect(logger, redirectUrl);
        return await Task.FromResult(Results.Redirect(redirectUrl)).ConfigureAwait(false);
    }

    [LoggerMessage(Level = LogLevel.Debug, Message = "Device verification: redirecting to '{Path}'")]
    private static partial void LogDeviceVerificationRedirect(ILogger logger, string path);
}
