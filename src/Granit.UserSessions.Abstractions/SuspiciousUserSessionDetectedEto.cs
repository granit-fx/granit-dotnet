using Granit.Events;

namespace Granit.UserSessions;

/// <summary>
/// Integration event raised when a session is assessed as <c>Medium</c> or <c>High</c> risk. Consumers
/// (notifications, step-up authentication) subscribe to react.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.UserSessions.Abstractions</c> so a subscriber can react to suspicious sessions without
/// referencing the anomaly-detection engine (and transitively <c>Granit.AI</c>).
/// </remarks>
/// <param name="UserId">Subject the session belongs to.</param>
/// <param name="SessionId">The flagged session.</param>
/// <param name="Category">Primary reason code (e.g. <c>"impossible_travel"</c>).</param>
/// <param name="RiskScore">Normalized risk score in <c>[0, 1]</c>.</param>
/// <param name="DetectedAt">When the verdict was produced.</param>
public sealed record SuspiciousUserSessionDetectedEto(
    string UserId,
    string SessionId,
    string Category,
    double RiskScore,
    DateTimeOffset DetectedAt) : IIntegrationEvent;
