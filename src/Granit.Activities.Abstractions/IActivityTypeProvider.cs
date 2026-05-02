namespace Granit.Activities;

/// <summary>
/// Cross-module hook for contributing additional activity types to the framework
/// catalog (per ADR-045 IoC contributor pattern, third application after
/// <c>IWorkspaceContributor</c> and <c>IEntityRelationContributor</c>). Each
/// implementation is registered via
/// <c>services.AddActivityTypeProvider&lt;T&gt;()</c>; the runtime module
/// (story A2) aggregates every registered provider's contributions into the
/// canonical <see cref="IActivityRegistry"/>.
/// </summary>
/// <remarks>
/// Used so that, for example, <c>Granit.Sales</c> can add a <c>"Quote"</c>
/// activity type and <c>Granit.Identity</c> can add an <c>"Onboarding"</c>
/// type without either side taking a runtime dependency on the other — both
/// pull only <c>Granit.Activities.Abstractions</c>. Activity types from
/// providers that are not loaded silently drop (entities that opt into them
/// via <c>AllowedTypes(...)</c> simply never see the option).
/// </remarks>
public interface IActivityTypeProvider
{
    /// <summary>
    /// Returns the activity types this provider contributes. Called once at
    /// registry construction; the result MUST be deterministic — providers
    /// SHOULD NOT vary their output based on time, request state, or any
    /// other ambient context.
    /// </summary>
    IEnumerable<ActivityType> Provide();
}
