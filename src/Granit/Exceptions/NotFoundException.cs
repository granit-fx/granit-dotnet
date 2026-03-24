namespace Granit.Exceptions;

/// <summary>
/// Exception thrown when a requested resource does not exist.
/// Maps to <c>404 Not Found</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exception intentionally does NOT implement <see cref="IHasErrorCode"/> to prevent
/// leaking internal resource types or identifiers to unauthenticated callers.
/// The generic 404 message is a security best practice (OWASP).
/// </para>
/// <para>
/// Prefer the more specific <see cref="EntityNotFoundException"/> for domain aggregate lookups.
/// Use this base class for non-entity resources (files, blobs, external references)
/// that require a 404 response without leaking domain schema information.
/// </para>
/// </remarks>
public class NotFoundException : Exception, IUserFriendlyException
{
    /// <summary>
    /// Initializes a new instance of <see cref="NotFoundException"/>.
    /// </summary>
    /// <param name="message">Human-readable message safe for client display.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public NotFoundException(string? message = null, Exception? innerException = null)
        : base(message ?? "The requested resource was not found.", innerException)
    {
    }
}
