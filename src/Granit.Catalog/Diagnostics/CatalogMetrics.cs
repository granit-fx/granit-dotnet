using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Granit.Catalog.Diagnostics;

/// <summary>
/// OpenTelemetry metrics for the catalog module.
/// Meter: <c>Granit.Catalog</c>.
/// </summary>
public sealed class CatalogMetrics
{
    /// <summary>The meter name used for all catalog metrics.</summary>
    public const string MeterName = "Granit.Catalog";

    private const string TenantIdTag = "tenant_id";
    private const string GlobalTenant = "global";
    private const string ProviderTag = "provider";

    private readonly Counter<long> _productsCreated;
    private readonly Counter<long> _productsPublished;
    private readonly Counter<long> _productsArchived;
    private readonly Counter<long> _externalMappingsAdded;
    private readonly Counter<long> _externalMappingsRemoved;

    /// <summary>Initializes catalog metrics using the specified meter factory.</summary>
    public CatalogMetrics(IMeterFactory meterFactory)
    {
        ArgumentNullException.ThrowIfNull(meterFactory);

        Meter meter = meterFactory.Create(MeterName);

        _productsCreated = meter.CreateCounter<long>(
            "granit.catalog.product.created",
            description: "Number of products created (Draft).");

        _productsPublished = meter.CreateCounter<long>(
            "granit.catalog.product.published",
            description: "Number of products transitioned to Published.");

        _productsArchived = meter.CreateCounter<long>(
            "granit.catalog.product.archived",
            description: "Number of products transitioned to Archived.");

        _externalMappingsAdded = meter.CreateCounter<long>(
            "granit.catalog.product.external_mapping.added",
            description: "Number of external provider mappings added to products.");

        _externalMappingsRemoved = meter.CreateCounter<long>(
            "granit.catalog.product.external_mapping.removed",
            description: "Number of external provider mappings removed from products.");
    }

    /// <summary>Records a product creation.</summary>
    public void RecordProductCreated(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _productsCreated.Add(1, tags);
    }

    /// <summary>Records a product transition to Published.</summary>
    public void RecordProductPublished(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _productsPublished.Add(1, tags);
    }

    /// <summary>Records a product transition to Archived.</summary>
    public void RecordProductArchived(string? tenantId)
    {
        var tags = new TagList { { TenantIdTag, tenantId ?? GlobalTenant } };
        _productsArchived.Add(1, tags);
    }

    /// <summary>Records an external mapping addition. <paramref name="providerName"/> tags the metric.</summary>
    public void RecordExternalMappingAdded(string? tenantId, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { ProviderTag, providerName },
        };
        _externalMappingsAdded.Add(1, tags);
    }

    /// <summary>Records an external mapping removal. <paramref name="providerName"/> tags the metric.</summary>
    public void RecordExternalMappingRemoved(string? tenantId, string providerName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var tags = new TagList
        {
            { TenantIdTag, tenantId ?? GlobalTenant },
            { ProviderTag, providerName },
        };
        _externalMappingsRemoved.Add(1, tags);
    }
}
