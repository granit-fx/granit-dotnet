using Granit.Events;

namespace Granit.Identity;

/// <summary>
/// Integration event raised when a user answers "no, it wasn't me" to a suspicious-session alert. The sessions
/// have already been revoked by the time this publishes; subscribers perform the heavier remediation that is
/// backend-specific — notably <c>Granit.Identity.Local</c> forcing a credential reset. Federated identity
/// providers own their own credentials and ignore it.
/// </summary>
/// <remarks>
/// Lives in <c>Granit.Identity.Abstractions</c> so a remediation subscriber reacts without referencing the
/// endpoints layer. Published exactly once per review (the review endpoint is single-use), so a handler may treat
/// it as a one-shot trigger.
/// </remarks>
/// <param name="UserId">Subject who denied the session.</param>
/// <param name="SessionId">The denied session (already revoked, alongside the user's other sessions).</param>
/// <param name="TenantId">Tenant the user belongs to; consumers must establish this scope before acting.</param>
/// <param name="DeniedAt">When the user denied the session.</param>
public sealed record SessionDeniedEto(
    string UserId,
    string SessionId,
    Guid? TenantId,
    DateTimeOffset DeniedAt) : IIntegrationEvent;
