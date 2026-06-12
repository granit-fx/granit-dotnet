using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.OutputCaching;
using Microsoft.Net.Http.Headers;

namespace Granit.Http.OutputCaching.Policies;

/// <summary>
/// Output cache policy that keeps <em>private</em> (per-caller) responses out of the shared
/// output cache:
/// <list type="bullet">
///   <item>Requests carrying credentials are excluded from caching</item>
///   <item>Responses with <c>Set-Cookie</c> headers are never stored</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>
/// "Private" in the HTTP-caching sense (<c>Cache-Control: private</c>): a response intended
/// for a single user that a shared cache must not store. Registered as a singleton base
/// policy. A request is treated as personalised when it is authenticated
/// (<see cref="Microsoft.AspNetCore.Http.HttpContext.User"/>) or simply carries an
/// <c>Authorization</c> or <c>Cookie</c> header. This prevents one caller's response
/// (e.g. <c>/bff/user</c>) from being served to another — the unauthorized disclosure that
/// would otherwise breach data-protection rules (GDPR Art. 5/32) — but it is one isolation
/// measure, not "GDPR compliance" on its own.
/// </para>
/// <para>
/// Keying off the request's credentials rather than <c>User</c> alone is deliberate:
/// the output-cache middleware frequently runs before authentication populates
/// <c>User</c>, and cookie/session schemes (such as the BFF session cookie) authenticate
/// inside the endpoint and never set <c>User</c> at all. Without this, a per-user response
/// is stored in a shared, caller-agnostic entry and can be served to a different caller.
/// Endpoints that genuinely serve public, non-personalised content to cookie-bearing
/// callers should opt into an explicit caching policy with appropriate <c>VaryBy</c> rules.
/// </para>
/// </remarks>
internal sealed class PrivateResponseOutputCachePolicy : IOutputCachePolicy
{
    /// <inheritdoc/>
    public ValueTask CacheRequestAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        HttpRequest request = context.HttpContext.Request;

        bool carriesCredentials =
            context.HttpContext.User.Identity?.IsAuthenticated == true
            || request.Headers.ContainsKey(HeaderNames.Authorization)
            || request.Headers.ContainsKey(HeaderNames.Cookie);

        if (carriesCredentials)
        {
            context.EnableOutputCaching = false;
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc/>
    public ValueTask ServeFromCacheAsync(OutputCacheContext context, CancellationToken cancellation) =>
        ValueTask.CompletedTask;

    /// <inheritdoc/>
    public ValueTask ServeResponseAsync(OutputCacheContext context, CancellationToken cancellation)
    {
        if (context.HttpContext.Response.Headers.ContainsKey("Set-Cookie"))
        {
            context.AllowCacheStorage = false;
        }

        return ValueTask.CompletedTask;
    }
}
