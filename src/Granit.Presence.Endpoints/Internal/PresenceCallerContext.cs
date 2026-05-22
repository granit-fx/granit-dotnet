using System.Security.Claims;

namespace Granit.Presence.Endpoints.Internal;

internal static class PresenceCallerContext
{
    /// <summary>
    /// Extracts the caller's user identifier from the <see cref="ClaimsPrincipal"/>.
    /// Looks at <c>nameid</c> then <c>sub</c>. Throws when neither is present
    /// (defensive: the endpoint group requires authorization).
    /// </summary>
    public static Guid GetUserId(ClaimsPrincipal user)
    {
        ArgumentNullException.ThrowIfNull(user);

        string? raw =
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");

        if (raw is null)
        {
            throw new UnauthorizedAccessException("User identifier claim not found.");
        }

        return Guid.TryParse(raw, out Guid parsed)
            ? parsed
            : throw new UnauthorizedAccessException($"User identifier '{raw}' is not a valid GUID.");
    }
}
