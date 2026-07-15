namespace Granit.Identity.Local.Exceptions;

/// <summary>
/// Stable classification of an ASP.NET Identity operation error, derived from the
/// error <em>code</em> (not its localized description). Endpoints branch on this kind
/// instead of matching the human-readable message, which changes with the request culture.
/// </summary>
public enum IdentityOperationErrorKind
{
    /// <summary>The error code is not one the framework classifies (treat as a generic failure).</summary>
    Unknown = 0,

    /// <summary>The username collides with an existing account (<c>DuplicateUserName</c>).</summary>
    DuplicateUserName,

    /// <summary>The email collides with an existing account (<c>DuplicateEmail</c>).</summary>
    DuplicateEmail,

    /// <summary>The email is malformed (<c>InvalidEmail</c>).</summary>
    InvalidEmail,

    /// <summary>The username is malformed (<c>InvalidUserName</c>).</summary>
    InvalidUserName,

    /// <summary>The password fails the configured strength policy (any <c>Password*</c> code).</summary>
    PasswordPolicy,
}

/// <summary>A single ASP.NET Identity error, classified by <see cref="IdentityOperationErrorKind"/>.</summary>
/// <param name="Code">The raw ASP.NET Identity error code (e.g. <c>DuplicateEmail</c>).</param>
/// <param name="Kind">The stable classification of <paramref name="Code"/>.</param>
/// <param name="Description">The localized, human-readable description (server-side logging only — never surfaced on the wire).</param>
public sealed record IdentityOperationError(string Code, IdentityOperationErrorKind Kind, string Description);

/// <summary>
/// Thrown by the local ASP.NET Identity provider when a write operation
/// (create / update / role / password) is rejected by ASP.NET Identity's own validators.
/// </summary>
/// <remarks>
/// Carries the classified <see cref="Errors"/> so the HTTP layer maps to an
/// RFC 7807 problem with a <em>localized</em> detail — never the raw
/// <see cref="Exception.Message"/>, which leaks internal validator text.
/// Derives from <see cref="InvalidOperationException"/> for back-compat with existing
/// generic catch blocks.
/// </remarks>
public sealed class IdentityOperationException(string operation, IReadOnlyList<IdentityOperationError> errors)
    : InvalidOperationException($"{operation} failed: {string.Join(", ", errors.Select(e => e.Description))}")
{
    /// <summary>The operation that failed (e.g. <c>"User creation"</c>).</summary>
    public string Operation { get; } = operation;

    /// <summary>The classified errors returned by ASP.NET Identity.</summary>
    public IReadOnlyList<IdentityOperationError> Errors { get; } = errors;

    /// <summary>
    /// <see langword="true"/> when any error is a username/email uniqueness collision —
    /// the signal callers use to preserve anti-enumeration behaviour (silent success)
    /// or to return a 409 without revealing which field collided.
    /// </summary>
    public bool IsConflict => Errors.Any(e =>
        e.Kind is IdentityOperationErrorKind.DuplicateUserName or IdentityOperationErrorKind.DuplicateEmail);

    /// <summary><see langword="true"/> when any error stems from the password strength policy.</summary>
    public bool HasPasswordPolicyError => Errors.Any(e => e.Kind is IdentityOperationErrorKind.PasswordPolicy);
}
