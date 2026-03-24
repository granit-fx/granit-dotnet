using Granit.Exceptions;

namespace Granit.Features.Exceptions;

/// <summary>
/// Exception thrown when an operation requires a feature that is not enabled
/// for the current tenant's plan.
/// </summary>
/// <remarks>
/// Extends <see cref="ForbiddenException"/> and is automatically mapped to
/// <c>HTTP 403 Forbidden</c> by <c>GranitExceptionHandler</c>.
/// <para>
/// Error code convention: <c>"Features:NotEnabled"</c>.
/// Mapped to <c>ProblemDetails.Extensions["errorCode"]</c> for frontend routing
/// (e.g. displaying an upgrade modal).
/// </para>
/// </remarks>
public sealed class FeatureNotEnabledException : ForbiddenException, IHasErrorCode
{
    /// <summary>The name of the feature that is not enabled.</summary>
    public string FeatureName { get; }

    /// <inheritdoc/>
    public string ErrorCode => "Features:NotEnabled";

    /// <summary>
    /// Initializes a new instance of <see cref="FeatureNotEnabledException"/>.
    /// </summary>
    /// <param name="featureName">The name of the feature that is not enabled.</param>
    public FeatureNotEnabledException(string featureName)
        : base($"The feature '{featureName}' is not enabled for your current plan.")
    {
        FeatureName = featureName;
    }
}
