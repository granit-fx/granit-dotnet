namespace Granit.Wolverine;

/// <summary>
/// Marks a Wolverine message type as legitimately tenant-agnostic — the framework
/// will not require an <c>X-Tenant-Id</c> envelope header when dispatching to its
/// handlers.
/// </summary>
/// <remarks>
/// <para>
/// Most integration events flow within a single tenant context. The
/// <see cref="Behaviors.TenantContextBehavior"/> records every untenanted envelope
/// as <c>granit.wolverine.envelope.no_tenant</c> with <c>outcome=marked</c> when
/// the type carries this attribute, <c>unmarked</c> otherwise. The attribute is
/// purely declarative observability — authorization is enforced downstream by
/// per-(user, tenant, action) permission checks at the handler boundary.
/// </para>
/// <para>
/// Apply on the message record/class only when the message is intentionally
/// host-scope — system health checks, configuration broadcasts, scheduling tick
/// events. The marker is read by reflection at message-receive time.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CrossTenantMessageAttribute : Attribute;
