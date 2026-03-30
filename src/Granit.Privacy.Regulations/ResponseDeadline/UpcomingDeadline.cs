namespace Granit.Privacy.Regulations.ResponseDeadline;

/// <summary>
/// Represents an upcoming response deadline for a privacy request.
/// </summary>
/// <param name="RequestId">The privacy request identifier.</param>
/// <param name="RequestType">The type of privacy request.</param>
/// <param name="Deadline">The calculated deadline (UTC).</param>
public sealed record UpcomingDeadline(Guid RequestId, PrivacyRequestType RequestType, DateTimeOffset Deadline);
