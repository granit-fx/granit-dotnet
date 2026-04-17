using Granit.Domain;
using Granit.Subscriptions.Domain.ValueObjects;
using Granit.Subscriptions.Events;
using Granit.Workflow.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A subscription plan defining pricing, features, and billing configuration.
/// </summary>
/// <remarks>
/// <para>
/// Plans follow a lifecycle managed by <see cref="WorkflowLifecycleStatus"/>:
/// <c>Draft → Published → Archived</c>. Archived plans are no longer purchasable
/// but remain usable by existing subscribers.
/// </para>
/// <para>
/// <see cref="PlanFeatureValues"/> feeds into the <c>Granit.Features</c> cascade
/// via <c>IPlanFeatureStore</c>, enabling automatic feature resolution per plan.
/// </para>
/// </remarks>
public sealed class Plan : AuditedAggregateRoot, IWorkflowStateful
{
    private readonly List<PlanPrice> _prices = [];
    private readonly List<PlanFeatureValue> _planFeatures = [];
    private readonly List<PlanExternalMapping> _externalMappings = [];

    private Plan() { }

    /// <summary>Creates a new plan in <see cref="WorkflowLifecycleStatus.Draft"/> status.</summary>
    public static Plan Create(
        Guid id,
        string name,
        string? description,
        PricingModel pricingModel,
        BillingInterval defaultInterval,
        int? trialDays = null,
        int? seatLimit = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        return new Plan
        {
            Id = id,
            Name = name,
            Description = description,
            PricingModel = pricingModel,
            DefaultInterval = defaultInterval,
            TrialDays = trialDays,
            SeatLimit = seatLimit,
            LifecycleStatus = WorkflowLifecycleStatus.Draft,
            SortOrder = 0,
        };
    }

    /// <summary>Plan display name.</summary>
    public string Name { get; private set; } = string.Empty;

    /// <summary>Optional plan description.</summary>
    public string? Description { get; private set; }

    /// <summary>Pricing model (Flat, PerSeat, PerUnit, Tiered).</summary>
    public PricingModel PricingModel { get; private set; }

    /// <summary>Default billing interval for new subscriptions.</summary>
    public BillingInterval DefaultInterval { get; private set; }

    /// <summary>Number of free trial days. Null means no trial.</summary>
    public int? TrialDays { get; private set; }

    /// <summary>Maximum number of seats. Null means unlimited.</summary>
    public int? SeatLimit { get; private set; }

    /// <summary>Display ordering for plan selection UI.</summary>
    public int SortOrder { get; private set; }

    /// <summary>Current lifecycle status (Draft, Published, Archived).</summary>
    public WorkflowLifecycleStatus LifecycleStatus { get; private set; }

    /// <summary>Price points per currency and interval.</summary>
    public IReadOnlyList<PlanPrice> Prices => _prices.AsReadOnly();

    /// <summary>Feature values that feed into the <c>Granit.Features</c> cascade.</summary>
    public IReadOnlyList<PlanFeatureValue> PlanFeatureValues => _planFeatures.AsReadOnly();

    /// <summary>External provider mappings (Stripe, Mollie, etc.).</summary>
    public IReadOnlyList<PlanExternalMapping> ExternalMappings => _externalMappings.AsReadOnly();

    // ── IWorkflowStateful ──────────────────────────────────────────────

    static string IWorkflowStateful.StatusPropertyName => nameof(LifecycleStatus);

    static string IWorkflowStateful.WorkflowEntityType => "Plan";

    /// <inheritdoc />
    public string GetWorkflowEntityId() => Id.ToString();

    // ── Behavior methods ───────────────────────────────────────────────

    /// <summary>Updates plan metadata. Only allowed in Draft status.</summary>
    public void Update(string name, string? description, int sortOrder)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
        Description = description;
        SortOrder = sortOrder;
    }

    /// <summary>Adds a price point to this plan. Only allowed in Draft status.</summary>
    public void AddPrice(PlanPrice price)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(price);
        _prices.Add(price);
    }

    /// <summary>
    /// Adds a new price version for the same (currency, interval) slot, replacing the
    /// current active price. Allowed on Published plans (grandfathering / price versioning).
    /// </summary>
    /// <returns>The previous price that was replaced, or <c>null</c> if no matching active price existed.</returns>
    public PlanPrice? AddPriceVersion(PlanPrice newPrice, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(newPrice);

        if (LifecycleStatus == WorkflowLifecycleStatus.Archived)
        {
            throw new InvalidOperationException(
                $"Plan '{Id}' is Archived. Price versions cannot be added to archived plans.");
        }

        PlanPrice? currentPrice = _prices
            .FirstOrDefault(p =>
                p.IsCurrent
                && string.Equals(p.Currency, newPrice.Currency, StringComparison.OrdinalIgnoreCase)
                && p.Interval == newPrice.Interval);

        currentPrice?.MarkReplaced(newPrice.Id, now);

        _prices.Add(newPrice);

        AddDistributedEvent(new PlanPriceCreatedEto(
            Id, newPrice.Id, newPrice.Amount, newPrice.Currency,
            newPrice.Interval.ToString(), newPrice.EffectiveFrom));

        if (currentPrice is not null)
        {
            AddDistributedEvent(new PlanPriceReplacedEto(
                Id, currentPrice.Id, newPrice.Id, currentPrice.Amount,
                newPrice.Amount, newPrice.Currency, newPrice.Interval.ToString()));
        }

        return currentPrice;
    }

    /// <summary>Adds a feature value mapping to this plan.</summary>
    public void AddFeatureValue(PlanFeatureValue featureValue)
    {
        EnsureDraft();
        ArgumentNullException.ThrowIfNull(featureValue);
        _planFeatures.Add(featureValue);
    }

    /// <summary>Adds an external provider mapping.</summary>
    public void AddExternalMapping(PlanExternalMapping mapping)
    {
        ArgumentNullException.ThrowIfNull(mapping);
        _externalMappings.Add(mapping);
    }

    /// <summary>Publishes the plan, making it available for purchase.</summary>
    public void Publish()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Plan '{Id}' is in '{LifecycleStatus}' status. Only Draft plans can be published.");
        }

        if (_prices.Count == 0)
        {
            throw new InvalidOperationException(
                $"Plan '{Id}' must have at least one price before publishing.");
        }

        LifecycleStatus = WorkflowLifecycleStatus.Published;
    }

    /// <summary>Archives the plan, removing it from sale. Existing subscribers are unaffected.</summary>
    public void Archive()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Published)
        {
            throw new InvalidOperationException(
                $"Plan '{Id}' is in '{LifecycleStatus}' status. Only Published plans can be archived.");
        }

        LifecycleStatus = WorkflowLifecycleStatus.Archived;
    }

    /// <summary>Returns the current price for the given currency and interval (not replaced), or <c>null</c>.</summary>
    public PlanPrice? GetCurrentPrice(string currency, BillingInterval interval) =>
        _prices.FirstOrDefault(p =>
            p.IsCurrent
            && string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase)
            && p.Interval == interval);

    /// <summary>Returns all price versions for the given currency and interval, newest first.</summary>
    public IReadOnlyList<PlanPrice> GetPriceHistory(string currency, BillingInterval interval) =>
        _prices
            .Where(p =>
                string.Equals(p.Currency, currency, StringComparison.OrdinalIgnoreCase)
                && p.Interval == interval)
            .OrderByDescending(p => p.EffectiveFrom)
            .ToList();

    private void EnsureDraft()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Plan '{Id}' is in '{LifecycleStatus}' status. Only Draft plans can be modified.");
        }
    }
}
