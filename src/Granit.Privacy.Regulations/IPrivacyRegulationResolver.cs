namespace Granit.Privacy.Regulations;

/// <summary>
/// Resolves the applicable privacy regulation(s) for the current tenant context.
/// Scoped service — resolution depends on <see cref="Granit.MultiTenancy.ICurrentTenant"/>.
/// </summary>
public interface IPrivacyRegulationResolver
{
    /// <summary>
    /// Returns the primary regulation profile for the current tenant.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no regulation is configured for the current tenant and no default is set.
    /// </exception>
    Task<PrivacyRegulationProfile> ResolveAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns all applicable regulations for the current tenant context.
    /// Useful for audit/reporting when a tenant operates under multiple regulations.
    /// </summary>
    Task<IReadOnlyList<PrivacyRegulationProfile>> ResolveAllAsync(CancellationToken cancellationToken = default);
}
