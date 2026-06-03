using System.Diagnostics;
using Granit.MultiTenancy;
using Granit.Users;
using Wolverine;

namespace Granit.Wolverine.Middleware;

/// <summary>
/// Wolverine middleware that propagates the current tenant, user, and trace context
/// into outgoing message envelopes via standard headers.
/// </summary>
/// <remarks>
/// <para>
/// Injects <c>X-Tenant-Id</c> from <see cref="ICurrentTenant.Id"/>,
/// <c>X-User-Id</c> from <see cref="ICurrentUserService.UserId"/>, and
/// <c>traceparent</c> from <see cref="Activity.Current"/> (W3C Trace Context)
/// into the <see cref="Envelope.Headers"/> dictionary before the message exits the pipeline.
/// Headers are omitted when the corresponding context is absent.
/// </para>
/// <para>
/// Registered globally via <c>opts.Policies.AddMiddleware&lt;OutgoingContextMiddleware&gt;()</c>
/// in <c>AddGranitWolverine()</c>. Context is consumed by
/// <see cref="Granit.Wolverine.Behaviors.TenantContextBehavior"/>,
/// <see cref="Granit.Wolverine.Behaviors.UserContextBehavior"/>, and
/// <see cref="Granit.Wolverine.Behaviors.TraceContextBehavior"/> on the receiving side.
/// </para>
/// <para>
/// Compliance (GDPR Art. 5(1)(c) — data minimization): only the opaque <c>X-User-Id</c>
/// (sub claim) is propagated. First name / last name are intentionally excluded to avoid
/// storing PII in the outbox tables. Background handlers that need display names should
/// resolve them on demand from the identity store.
/// </para>
/// </remarks>
public sealed class OutgoingContextMiddleware(
    ICurrentTenant currentTenant,
    ICurrentUserService currentUserService)
{
    internal const string TenantIdHeader = "X-Tenant-Id";
    internal const string UserIdHeader = "X-User-Id";
    internal const string ActorKindHeader = "X-Actor-Kind";
#pragma warning disable GRSEC003 // HTTP header name constant, not a secret
    internal const string ApiKeyIdHeader = "X-Api-Key-Id";
#pragma warning restore GRSEC003
    internal const string TraceParentHeader = "traceparent";

    /// <summary>
    /// Injects tenant, user, and trace context headers into <paramref name="envelope"/> before dispatch.
    /// </summary>
    /// <param name="envelope">The outgoing Wolverine envelope.</param>
    public void Before(Envelope envelope)
    {
        if (currentTenant.Id.HasValue)
        {
            string tenantId = currentTenant.Id.Value.ToString();
            envelope.Headers[TenantIdHeader] = tenantId;

            // Mirror into Wolverine's native tenant slot (in addition to the custom header) so
            // wolverine.* spans/metrics and any native multi-tenant routing observe the tenant
            // without parsing X-Tenant-Id. TenantContextBehavior still restores ICurrentTenant
            // from the header — this is purely additive for native observability.
            envelope.TenantId = tenantId;
        }

        if (currentUserService.IsAuthenticated && currentUserService.UserId is { Length: > 0 } userId)
        {
            envelope.Headers[UserIdHeader] = userId;
        }

        // Propagate actor kind (User, ExternalSystem, System)
        ActorKind actorKind = currentUserService.ActorKind;
        if (actorKind != ActorKind.User)
        {
            envelope.Headers[ActorKindHeader] = actorKind.ToString();
        }

        if (currentUserService.ApiKeyId is { } apiKeyId)
        {
            envelope.Headers[ApiKeyIdHeader] = apiKeyId.ToString();
        }

        string? traceParent = Activity.Current?.Id;
        if (traceParent is not null)
        {
            envelope.Headers[TraceParentHeader] = traceParent;
        }
    }
}
