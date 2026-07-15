namespace Granit.Identity.Local.Exceptions;

/// <summary>
/// Thrown by the local ASP.NET Identity provider when an operation targets a user id
/// that no longer resolves to a stored account.
/// </summary>
/// <remarks>
/// Derives from <see cref="InvalidOperationException"/> so pre-existing
/// <c>catch (InvalidOperationException)</c> blocks keep functioning; new endpoint code
/// catches this type directly instead of string-matching the exception message
/// (which is culture-dependent once a localized <c>IdentityErrorDescriber</c> is wired).
/// </remarks>
public sealed class IdentityUserNotFoundException(string userId)
    : InvalidOperationException($"User '{userId}' not found.")
{
    /// <summary>The user id that failed to resolve.</summary>
    public string UserId { get; } = userId;
}
