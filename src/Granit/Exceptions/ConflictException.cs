namespace Granit.Exceptions;

/// <summary>
/// Exception representing a business-level conflict (e.g. duplicate resource, concurrency violation).
/// Maps to <c>409 Conflict</c>.
/// </summary>
/// <remarks>
/// Implements <see cref="IUserFriendlyException"/>: the message is safe to expose to clients.
/// Implements <see cref="IHasErrorCode"/>: the code can be used for localization lookup.
/// </remarks>
/// <example>
/// <code>
/// throw new ConflictException("Patient:DuplicateNationalId", "A patient with this national ID already exists.");
/// </code>
/// </example>
public class ConflictException : Exception, IHasErrorCode, IUserFriendlyException
{
    /// <inheritdoc/>
    public string ErrorCode { get; }

    /// <summary>
    /// Initializes a new instance of <see cref="ConflictException"/>.
    /// </summary>
    /// <param name="errorCode">
    /// Structured error code (e.g. <c>"Patient:DuplicateNationalId"</c>).
    /// </param>
    /// <param name="message">Human-readable message safe for client display. Defaults to the error code.</param>
    /// <param name="innerException">Optional inner exception (never forwarded to the client).</param>
    public ConflictException(string errorCode, string? message = null, Exception? innerException = null)
        : base(message ?? errorCode, innerException)
    {
        ErrorCode = errorCode;
    }
}
