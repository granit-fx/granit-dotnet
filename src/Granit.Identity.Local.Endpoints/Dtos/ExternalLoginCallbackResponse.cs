namespace Granit.Identity.Local.Endpoints.Dtos;

/// <summary>
/// Response DTO for the external login OAuth callback.
/// </summary>
/// <param name="Status">
/// <see cref="StatusCompleted"/> when an account was authenticated (existing or newly created and a
/// session established), or <see cref="StatusNeedsProfileCompletion"/> when the provider returned
/// insufficient data and the user must complete the pre-filled registration form.
/// </param>
/// <param name="UserId">The authenticated/created user identifier; <see langword="null"/> when profile completion is required.</param>
/// <param name="IsNewUser">Whether a new account was created during the callback.</param>
/// <param name="ContinuationToken">
/// The opaque, signed token to pass back to the complete-registration endpoint; non-null only when
/// profile completion is required. Carries the provider key — never exposed in plaintext.
/// </param>
/// <param name="Prefill">Provider data to seed the registration form; non-null only when profile completion is required.</param>
public sealed record ExternalLoginCallbackResponse(
    string Status,
    Guid? UserId,
    bool IsNewUser,
    string? ContinuationToken,
    ExternalProfilePrefillResponse? Prefill)
{
    /// <summary>Status value: an account was authenticated and a session was established.</summary>
    public const string StatusCompleted = "completed";

    /// <summary>Status value: the user must complete the pre-filled registration form.</summary>
    public const string StatusNeedsProfileCompletion = "needs-profile-completion";
}

/// <summary>
/// Non-sensitive provider data surfaced to the client to pre-fill the registration form.
/// </summary>
/// <param name="Email">The email returned by the provider, if any.</param>
/// <param name="FirstName">The first name returned by the provider, if any.</param>
/// <param name="LastName">The last name returned by the provider, if any.</param>
public sealed record ExternalProfilePrefillResponse(
    string? Email,
    string? FirstName,
    string? LastName);
