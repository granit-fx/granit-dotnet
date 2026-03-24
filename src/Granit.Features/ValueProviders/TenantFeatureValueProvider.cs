using Granit.Features.Definitions;
using Granit.MultiTenancy;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Features.ValueProviders;

/// <summary>
/// Resolves tenant-specific feature overrides from <see cref="IFeatureStoreReader"/>.
/// Runs first in the cascade (order = 100) — highest priority.
/// </summary>
/// <remarks>
/// Returns <c>null</c> when no tenant context is available (host / global requests)
/// or when <see cref="ICurrentTenant"/> is not registered (single-tenant applications).
/// </remarks>
internal sealed class TenantFeatureValueProvider(
    IServiceProvider serviceProvider,
    IFeatureStoreReader featureStoreReader) : IFeatureValueProvider
{
    private readonly IServiceProvider _serviceProvider = serviceProvider;
    private readonly IFeatureStoreReader _featureStoreReader = featureStoreReader;

    /// <inheritdoc/>
    public string Name => "Tenant";

    /// <inheritdoc/>
    public int Order => 100;

    /// <inheritdoc/>
    public async Task<string?> GetOrNullAsync(FeatureDefinition definition, CancellationToken cancellationToken = default)
    {
        ICurrentTenant? currentTenant = _serviceProvider.GetService<ICurrentTenant>();
        if (currentTenant is null || !currentTenant.IsAvailable)
        {
            return null;
        }

        string tenantId = currentTenant.Id!.Value.ToString();
        return await _featureStoreReader.GetOrNullAsync(definition.Name, tenantId, cancellationToken).ConfigureAwait(false);
    }
}
