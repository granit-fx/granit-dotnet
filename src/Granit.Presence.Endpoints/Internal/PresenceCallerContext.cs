using System.Diagnostics.CodeAnalysis;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;

namespace Granit.Presence.Endpoints.Internal;

internal static class PresenceCallerContext
{
    /// <summary>
    /// Attempts to extract the caller's user identifier from the <see cref="ClaimsPrincipal"/>.
    /// Looks at <c>nameid</c> then <c>sub</c>. Returns <c>null</c> when neither is present
    /// or when the value is not a valid <see cref="Guid"/>.
    /// </summary>
    public static Guid? TryGetUserId(ClaimsPrincipal? user)
    {
        if (user is null)
        {
            return null;
        }

        string? raw =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        return Guid.TryParse(raw, out Guid parsed) ? parsed : null;
    }

    /// <summary>
    /// Returns <c>true</c> and the caller's user identifier when a valid GUID claim is present,
    /// otherwise yields a 401 ProblemDetails result without echoing the raw claim value.
    /// </summary>
    public static bool TryResolveSelf(
        ClaimsPrincipal? user,
        out Guid userId,
        [NotNullWhen(false)] out ProblemHttpResult? unauthorized)
    {
        Guid? resolved = TryGetUserId(user);
        if (resolved is { } id)
        {
            userId = id;
            unauthorized = null;
            return true;
        }

        userId = Guid.Empty;
        unauthorized = TypedResults.Problem(
            detail: "Authenticated identity does not carry a recognized user identifier.",
            statusCode: StatusCodes.Status401Unauthorized,
            title: "Unauthorized");
        return false;
    }
}
