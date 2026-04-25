using System.Text.Json;
using Granit.Catalog.Events;
using Granit.Domain;
using Granit.Workflow.Domain;

namespace Granit.Catalog.Domain;

/// <summary>
/// A product in the catalog — the "thing being sold". Shared join key between
/// Metering (<c>MeterDefinition.ProductId</c>) and Subscriptions (<c>PlanPrice.ProductId</c>).
/// </summary>
/// <remarks>
/// <para>
/// Lifecycle managed by <see cref="WorkflowLifecycleStatus"/>:
/// <c>Draft → Published → Archived</c>. Only Published products may be referenced
/// by active price lists and meters; archived products keep their references intact
/// for audit trails but are no longer purchasable.
/// </para>
/// <para>
/// Implements <see cref="IHasMetadata"/> for free-form key/value attributes
/// (Stripe-style metadata, integration sync attributes, ...). Use the extension methods
/// in <see cref="MetadataExtensions"/> for typed read/write of individual entries,
/// or <see cref="ReplaceMetadata(IReadOnlyDictionary{string, string})"/> for bulk
/// replacement (used by the admin endpoint).
/// </para>
/// <para>
/// MVP scope: Host-owned (no <see cref="IMultiTenant"/>) — mirroring <c>Plan</c>.
/// The future e-commerce phase may extend this with multi-tenant scoping
/// per ADR 032.
/// </para>
/// </remarks>
public sealed class Product : AuditedAggregateRoot, IWorkflowStateful, IHasMetadata
{
    private readonly List<ProductExternalMapping> _externalMappings = [];

    private Product() { }

    /// <summary>Creates a new product in <see cref="WorkflowLifecycleStatus.Draft"/> status.</summary>
    public static Product Create(
        Guid id,
        string sku,
        string name,
        ProductType type,
        string unit,
        string? description = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sku);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        var product = new Product
        {
            Id = id,
            Sku = sku,
            Name = name,
            Type = type,
            Unit = unit,
            Description = description,
            LifecycleStatus = WorkflowLifecycleStatus.Draft,
        };

        product.AddDomainEvent(new ProductCreatedEvent(id, sku, name, type));
        return product;
    }

    /// <summary>Stable business identifier (unique per Host catalog).</summary>
    public string Sku { get; private set; } = string.Empty;

    /// <summary>Human-readable product name (shown in admin UI, invoice line items).</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional longer description.</summary>
    public string? Description { get; private set; }

    /// <summary>Product type — drives downstream behavior (tax, shipping, metering).</summary>
    public ProductType Type { get; private set; }

    /// <summary>
    /// Unit of measure (e.g., <c>"call"</c>, <c>"GB"</c>, <c>"seat"</c>, <c>"license"</c>).
    /// Free text; not enumerated to allow per-domain vocabulary.
    /// </summary>
    public string Unit { get; private set; } = string.Empty;

    /// <summary>Current lifecycle status (Draft, PendingReview, Published, Archived).</summary>
    public WorkflowLifecycleStatus LifecycleStatus { get; private set; }

    /// <inheritdoc />
    /// <remarks>
    /// MUST NOT contain PII (audit logs, exports, and the SQL column itself surface this content).
    /// EF Core persists this as a JSON string; the <c>MetadataSyncInterceptor</c> handles
    /// promotion to Shadow Properties when configured via <c>MapProperty&lt;T&gt;()</c>.
    /// </remarks>
    public string? MetadataJson { get; set; }

    /// <summary>External provider mappings (Stripe, Avalara, Odoo, etc.).</summary>
    public IReadOnlyList<ProductExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

    // ── IWorkflowStateful ──────────────────────────────────────────────

    static string IWorkflowStateful.StatusPropertyName => nameof(LifecycleStatus);

    static string IWorkflowStateful.WorkflowEntityType => "Product";

    /// <inheritdoc />
    public string GetWorkflowEntityId() => Id.ToString();

    // ── Behavior methods ───────────────────────────────────────────────

    /// <summary>Updates editable fields. Allowed only in <see cref="WorkflowLifecycleStatus.Draft"/>.</summary>
    /// <remarks>
    /// <see cref="Sku"/> and <see cref="Type"/> are immutable post-creation: changing
    /// either reshapes downstream contracts (invoice line item provenance, tax codes)
    /// and must be modeled as a new product with a fresh SKU.
    /// </remarks>
    public void Update(string name, string? description, string unit)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(unit);

        Name = name;
        Description = description;
        Unit = unit;
    }

    /// <summary>
    /// Replaces all extra properties at once. Use the framework extension methods
    /// (<see cref="MetadataExtensions.SetMetadataValue(IHasMetadata, string, string?)"/>)
    /// for granular per-key updates. Allowed in any lifecycle state.
    /// </summary>
    public void ReplaceMetadata(IReadOnlyDictionary<string, string> properties)
    {
        ArgumentNullException.ThrowIfNull(properties);

        MetadataJson = properties.Count > 0
            ? JsonSerializer.Serialize(properties)
            : null;
    }

    /// <summary>Publishes the product, making it available for use by Subscriptions and Metering.</summary>
    public void Publish()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Product '{Id}' is in '{LifecycleStatus}' status. Only Draft products can be published.");
        }

        LifecycleStatus = WorkflowLifecycleStatus.Published;
        AddDomainEvent(new ProductPublishedEvent(Id, Sku));
    }

    /// <summary>Archives the product. Existing references (meters, prices) are unaffected.</summary>
    public void Archive()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Published)
        {
            throw new InvalidOperationException(
                $"Product '{Id}' is in '{LifecycleStatus}' status. Only Published products can be archived.");
        }

        LifecycleStatus = WorkflowLifecycleStatus.Archived;
        AddDomainEvent(new ProductArchivedEvent(Id, Sku));
    }

    /// <summary>Adds an external provider mapping. Allowed in any lifecycle state.</summary>
    public void AddExternalMapping(ProductExternalMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _externalMappings.Add(mapping);
    }

    /// <summary>Removes an external provider mapping by id. Returns whether a mapping was removed.</summary>
    public bool RemoveExternalMapping(Guid mappingId)
    {
        int removed = _externalMappings.RemoveAll(m => m.Id == mappingId);
        return removed > 0;
    }

    private void EnsureDraft()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Product '{Id}' is in '{LifecycleStatus}' status. Only Draft products can be modified.");
        }
    }
}
