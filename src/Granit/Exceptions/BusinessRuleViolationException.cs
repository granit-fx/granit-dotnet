namespace Granit.Exceptions;

/// <summary>
/// Exception thrown when a business rule is violated by an otherwise well-formed request.
/// Maps to <c>422 Unprocessable Entity</c>.
/// </summary>
/// <remarks>
/// Use this exception when the request format is valid but violates domain invariants
/// (e.g. "the appointment slot is already taken", "the prescription quota is exceeded").
/// Prefer <see cref="ConflictException"/> for uniqueness violations (duplicate resource)
/// and <see cref="BusinessException"/> for generic bad-request scenarios.
/// <para>
/// Inherits <see cref="IHasErrorCode"/> and <see cref="IUserFriendlyException"/> from
/// <see cref="BusinessException"/>: the error code and message are safe to expose to clients.
/// </para>
/// </remarks>
/// <example>
/// <code>
/// throw new BusinessRuleViolationException("Appointment:SlotUnavailable");
/// throw new BusinessRuleViolationException("Prescription:QuotaExceeded", "Monthly prescription quota reached.");
/// </code>
/// </example>
public sealed class BusinessRuleViolationException : BusinessException
{
    /// <summary>
    /// Initializes a new instance of <see cref="BusinessRuleViolationException"/>.
    /// </summary>
    /// <param name="errorCode">
    /// Structured error code (e.g. <c>"Appointment:SlotUnavailable"</c>).
    /// Used for localization and client-side error handling.
    /// </param>
    /// <param name="message">
    /// Human-readable message safe for client display. Defaults to the error code.
    /// </param>
    /// <param name="innerException">Optional inner exception (never forwarded to the client).</param>
    public BusinessRuleViolationException(string errorCode, string? message = null, Exception? innerException = null)
        : base(errorCode, message, innerException)
    {
    }
}
