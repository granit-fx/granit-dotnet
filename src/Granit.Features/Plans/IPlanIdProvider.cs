namespace Granit.Features.Plans;

/// <summary>
/// Returns the commercial plan identifier associated with the current request context.
/// </summary>
/// <remarks>
/// Implement and register this interface to activate plan-level feature resolution.
/// The implementation typically reads the plan from the current tenant's subscription
/// (e.g., via Stripe or your billing service).
/// <para>
/// Registration:
/// <c>services.AddScoped&lt;IPlanIdProvider, YourPlanIdProvider&gt;();</c>
/// </para>
/// </remarks>
public interface IPlanIdProvider
{
    /// <summary>
    /// Returns the plan identifier (e.g., <c>"starter"</c>, <c>"premium"</c>,
    /// <c>"enterprise"</c>), or <c>null</c> if no plan applies to the current context.
    /// </summary>
    Task<string?> GetCurrentPlanIdAsync(CancellationToken cancellationToken = default);
}
