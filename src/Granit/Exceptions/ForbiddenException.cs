namespace Granit.Exceptions;

/// <summary>
/// Exception thrown when an authenticated user attempts an operation they are not authorized to perform.
/// Maps to <c>403 Forbidden</c>.
/// </summary>
/// <remarks>
/// <para>
/// This exception intentionally does NOT implement <see cref="IHasErrorCode"/> to prevent
/// leaking internal permission schema or resource names to unauthenticated/unauthorized callers.
/// The generic 403 message is a security best practice (OWASP).
/// </para>
/// <para>
/// Use this exception for explicit authorization failures within application logic
/// (e.g. multi-tenancy boundary violations, resource ownership checks).
/// ASP.NET Core's authorization middleware handles most 403 cases automatically;
/// this exception is for cases that reach domain or application service code.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// if (appointment.TenantId != currentUser.TenantId)
///     throw new ForbiddenException("Access to this appointment is not allowed.");
/// </code>
/// </example>
public class ForbiddenException : Exception, IUserFriendlyException
{
    /// <summary>
    /// Initializes a new instance of <see cref="ForbiddenException"/> with an optional message.
    /// </summary>
    /// <param name="message">Human-readable message safe for client display.</param>
    /// <param name="innerException">Optional inner exception.</param>
    public ForbiddenException(string? message = null, Exception? innerException = null)
        : base(message ?? "Access to this resource is forbidden.", innerException)
    {
    }
}
