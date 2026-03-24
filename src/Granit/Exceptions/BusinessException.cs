namespace Granit.Exceptions;

/// <summary>
/// Exception representing a violated business rule.
/// Maps to <c>400 Bad Request</c>.
/// </summary>
/// <remarks>
/// Implements <see cref="IUserFriendlyException"/>: the message is safe to expose to clients.
/// Implements <see cref="IHasErrorCode"/>: the code can be used for localization lookup.
/// </remarks>
/// <example>
/// <code>
/// throw new BusinessException("Appointment:SlotUnavailable", "The requested time slot is no longer available.");
/// </code>
/// </example>
public class BusinessException : Exception, IHasErrorCode, IUserFriendlyException
{
    /// <inheritdoc/>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="BusinessException"/>.
    /// </summary>
    /// <param name="errorCode">
    /// Structured error code (e.g. <c>"Appointment:SlotUnavailable"</c>).
    /// Used for localization and client-side error handling.
    /// </param>
    /// <param name="message">
    /// Human-readable message safe for client display. Defaults to the error code.
    /// </param>
    /// <param name="innerException">Optional inner exception (never forwarded to the client).</param>
    public BusinessException(string errorCode, string? message = null, Exception? innerException = null)
        : base(message ?? errorCode, innerException)
    {
        ErrorCode = errorCode;
    }
}
