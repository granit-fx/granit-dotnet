using System.Diagnostics;

namespace Granit.Identity.AnomalyDetection.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.Identity.AnomalyDetection</c> distributed tracing.
/// </summary>
internal static class IdentityAnomalyDetectionActivitySource
{
    /// <summary>The activity source name.</summary>
    internal const string Name = "Granit.Identity.AnomalyDetection";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
