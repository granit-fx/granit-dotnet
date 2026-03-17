namespace Granit.Http.ExceptionHandling;

/// <summary>
/// Resolves the HTTP status code for a given exception.
/// </summary>
/// <remarks>
/// Implementations are registered as <c>IEnumerable&lt;IExceptionStatusCodeMapper&gt;</c>
/// and evaluated in registration order by <c>GranitExceptionHandler</c>.
/// The first non-null result wins (chain of responsibility).
/// </remarks>
/// <example>
/// Custom mapper in Granit.Persistence:
/// <code>
/// internal sealed class EfCoreExceptionStatusCodeMapper : IExceptionStatusCodeMapper
/// {
///     public int? TryGetStatusCode(Exception exception) => exception switch
///     {
///         DbUpdateConcurrencyException => StatusCodes.Status409Conflict,
///         _ => null
///     };
/// }
/// </code>
/// </example>
public interface IExceptionStatusCodeMapper
{
    /// <summary>
    /// Attempts to resolve the HTTP status code for the given exception.
    /// </summary>
    /// <param name="exception">The exception to map.</param>
    /// <returns>
    /// The HTTP status code, or <c>null</c> if this mapper does not handle the exception type.
    /// </returns>
    int? TryGetStatusCode(Exception exception);
}
