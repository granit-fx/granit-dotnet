namespace Granit.Exceptions;

/// <summary>
/// Indicates that an exception carries a structured error code.
/// The code is used by <c>GranitExceptionHandler</c> to populate
/// <c>ProblemDetails.Extensions["errorCode"]</c> and optionally resolve
/// a localized title via <c>IStringLocalizer</c>.
/// </summary>
/// <example>
/// Error code convention: <c>"Module:ErrorName"</c> (e.g. <c>"Vault:CredentialsFailed"</c>).
/// </example>
public interface IHasErrorCode
{
    /// <summary>
    /// Structured error code identifying the error type.
    /// Convention: <c>"Module:ErrorName"</c>.
    /// </summary>
    string ErrorCode { get; }
}
