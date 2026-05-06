namespace Granit.Wolverine;

/// <summary>
/// Marks a Wolverine message type as legitimately tenant-agnostic — the framework
/// will not require an <c>X-Tenant-Id</c> envelope header when dispatching to its
/// handlers.
/// </summary>
/// <remarks>
/// <para>
/// SECURITY: most integration events flow within a single tenant context, and a
/// missing tenant header is therefore a sign of a producer-side bug or an
/// envelope-forgery attempt. The
/// <see cref="Behaviors.TenantContextBehavior"/> records every untenanted envelope
/// as <c>granit.wolverine.envelope.no_tenant</c> for SOC observability, and —
/// when <see cref="Options.WolverineMessagingOptions.RequireEnvelopeTenant"/> is
/// enabled — rejects them as an exception.
/// </para>
/// <para>
/// Apply this attribute on the message record/class (<see cref="AttributeTargets.Class"/>
/// or <see cref="AttributeTargets.Struct"/>) only when the message is intentionally
/// host-scope — system health checks, configuration broadcasts, scheduling tick
/// events. The marker is read by reflection at message-receive time.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public sealed class CrossTenantMessageAttribute : Attribute;
