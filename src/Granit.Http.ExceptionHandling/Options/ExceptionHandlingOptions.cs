namespace Granit.Http.ExceptionHandling.Options;

/// <summary>
/// Configuration options for <see cref="GranitExceptionHandler"/>.
/// </summary>
public sealed class ExceptionHandlingOptions
{
    /// <summary>
    /// When <c>true</c>, the original exception message is included in the
    /// <c>ProblemDetails.Detail</c> field even for <c>5xx</c> errors.
    /// <para>
    /// <b>Default: <c>false</c>.</b> Should only be set to <c>true</c> in Development
    /// or staging environments.
    /// </para>
    /// <para>
    /// <b>ISO 27001 rule:</b> NEVER set to <c>true</c> in production. Internal error messages
    /// may contain PHI, SQL fragments, or internal paths.
    /// </para>
    /// </summary>
    public bool ExposeInternalErrorDetails { get; set; }
}
