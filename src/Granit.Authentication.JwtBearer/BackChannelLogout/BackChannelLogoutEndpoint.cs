using Granit.Authentication.JwtBearer.Options;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Granit.Authentication.JwtBearer.BackChannelLogout;

/// <summary>
/// Minimal API handler for the OIDC back-channel logout endpoint.
/// Receives a <c>logout_token</c> via <c>application/x-www-form-urlencoded</c> POST
/// and revokes the session in the distributed cache.
/// </summary>
internal static partial class BackChannelLogoutEndpoint
{
    /// <summary>
    /// Handles the back-channel logout POST request from the identity provider.
    /// </summary>
    public static async Task<Results<Ok, ProblemHttpResult>> HandleAsync(
        HttpRequest request,
        BackChannelLogoutTokenValidator validator,
        [FromServices] IRevokedSessionStore store,
        [FromServices] IOptions<JwtBearerAuthOptions> options,
        [FromServices] ILogger<BackChannelLogoutTokenValidator> logger,
        CancellationToken cancellationToken)
    {
        if (!request.HasFormContentType)
        {
            LogInvalidContentType(logger);
            return TypedResults.Problem(
                detail: "Expected application/x-www-form-urlencoded content type.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        IFormCollection form = await request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        string logoutToken = form["logout_token"].ToString();

        if (string.IsNullOrEmpty(logoutToken))
        {
            LogMissingLogoutToken(logger);
            return TypedResults.Problem(
                detail: "Missing 'logout_token' form parameter.",
                statusCode: StatusCodes.Status400BadRequest);
        }

        BackChannelLogoutResult result = await validator.ValidateAsync(logoutToken, cancellationToken).ConfigureAwait(false);
        if (!result.Success)
        {
            return TypedResults.Problem(
                detail: result.Error,
                statusCode: StatusCodes.Status400BadRequest);
        }

        TimeSpan ttl = options.Value.BackChannelLogout.SessionRevocationTtl;

        // Prefer sid (session-specific), fall back to sub (user-wide)
        string sessionKey = result.SessionId ?? result.SubjectId!;
        await store.RevokeSessionAsync(sessionKey, ttl, cancellationToken).ConfigureAwait(false);

        return TypedResults.Ok();
    }

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout request rejected: invalid content type.")]
    private static partial void LogInvalidContentType(ILogger logger);

    [LoggerMessage(Level = LogLevel.Warning, Message = "Back-channel logout request rejected: missing 'logout_token' parameter.")]
    private static partial void LogMissingLogoutToken(ILogger logger);
}
