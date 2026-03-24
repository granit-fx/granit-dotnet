using Granit.Exceptions;

namespace Granit.Features.Exceptions;

/// <summary>
/// Exception thrown when an operation would exceed the numeric limit
/// defined by the current tenant's plan for a given feature.
/// </summary>
/// <remarks>
/// Extends <see cref="ForbiddenException"/> and is automatically mapped to
/// <c>HTTP 403 Forbidden</c> by <c>GranitExceptionHandler</c>.
/// <para>
/// Error code convention: <c>"Features:LimitExceeded"</c>.
/// Carries <see cref="Current"/> and <see cref="Limit"/> for structured client responses.
/// </para>
/// </remarks>
public sealed class FeatureLimitExceededException : ForbiddenException, IHasErrorCode
{
    /// <summary>The name of the feature whose limit was exceeded.</summary>
    public string FeatureName { get; }

    /// <summary>The current count at the time the limit was checked.</summary>
    public long Current { get; }

    /// <summary>The maximum allowed value defined by the plan.</summary>
    public long Limit { get; }

    /// <inheritdoc/>
    public string ErrorCode => "Features:LimitExceeded";

    /// <summary>
    /// Initializes a new instance of <see cref="FeatureLimitExceededException"/>.
    /// </summary>
    /// <param name="featureName">The name of the exceeded feature.</param>
    /// <param name="current">The current count.</param>
    /// <param name="limit">The plan limit.</param>
    public FeatureLimitExceededException(string featureName, long current, long limit)
        : base($"The limit for feature '{featureName}' has been reached: {current}/{limit}. Upgrade your plan to increase this limit.")
    {
        FeatureName = featureName;
        Current = current;
        Limit = limit;
    }
}
