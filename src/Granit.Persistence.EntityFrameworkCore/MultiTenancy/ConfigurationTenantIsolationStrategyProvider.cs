using Microsoft.Extensions.Options;

namespace Granit.Persistence.EntityFrameworkCore.MultiTenancy;

/// <summary>
/// Returns a single statically configured <see cref="TenantIsolationStrategy"/> for all tenants,
/// read from <c>appsettings.json</c> section <c>TenantIsolation:Strategy</c>.
/// </summary>
/// <remarks>
/// <para>
/// Configuration example:
/// <code>
/// {
///   "TenantIsolation": {
///     "Strategy": "SchemaPerTenant"
///   }
/// }
/// </code>
/// Defaults to <see cref="TenantIsolationStrategy.SharedDatabase"/> when the section is absent.
/// </para>
/// <para>
/// An invalid value (e.g. <c>"Strategy": "Unknown"</c>) triggers a fail-fast
/// <see cref="OptionsValidationException"/> at application startup when <c>ValidateOnStart()</c>
/// is configured by <c>AddGranitIsolatedDbContext&lt;TContext&gt;()</c>.
/// </para>
/// </remarks>
internal sealed class ConfigurationTenantIsolationStrategyProvider(
    IOptions<TenantIsolationOptions> options) : ITenantIsolationStrategyProvider
{
    private readonly TenantIsolationStrategy _strategy = options.Value.Strategy;

    /// <inheritdoc/>
    public ValueTask<TenantIsolationStrategy> GetStrategyAsync(
        Guid? tenantId,
        CancellationToken cancellationToken = default) =>
        ValueTask.FromResult(_strategy);
}
