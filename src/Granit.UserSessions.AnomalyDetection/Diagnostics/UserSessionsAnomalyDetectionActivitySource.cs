using System.Diagnostics;

namespace Granit.UserSessions.AnomalyDetection.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for <c>Granit.UserSessions.AnomalyDetection</c> distributed tracing.
/// </summary>
internal static class UserSessionsAnomalyDetectionActivitySource
{
    /// <summary>The activity source name.</summary>
    internal const string Name = "Granit.UserSessions.AnomalyDetection";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);
}
