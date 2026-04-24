using Granit.Domain;

namespace Granit.Subscriptions.Domain;

/// <summary>
/// A price point for a plan in a specific currency and billing interval.
/// Supports versioning: when a price is replaced, <see cref="ReplacedByPriceId"/>
/// points to the successor and <see cref="ReplacedAt"/> records the timestamp.
/// </summary>
public sealed class PlanPrice : Entity
{
    private readonly List<PricingTier> _tiers = [];

    private PlanPrice() { }

    /// <summary>
    /// Creates a new plan price.
    /// <paramref name="productId"/> is an optional soft reference (no SQL FK across
    /// modules) to a <c>Granit.Catalog.Product</c> — the catalog item this price
    /// tarifs. Survives price versioning: a replaced price keeps its original
    /// <see cref="ProductId"/>, and the new version may carry the same or a different one.
    /// <paramref name="tieringMode"/> is required when the parent <c>Plan</c> uses
    /// <see cref="PricingModel.Tiered"/> and must be left null otherwise — the
    /// pairing is enforced by the validator on the create endpoint.
    /// </summary>
    public static PlanPrice Create(
        Guid id, decimal amount, string currency, BillingInterval interval,
        DateTimeOffset effectiveFrom, Guid? productId = null,
        TieringMode? tieringMode = null) =>
        new()
        {
            Id = id,
            Amount = amount,
            Currency = currency,
            Interval = interval,
            EffectiveFrom = effectiveFrom,
            ProductId = productId,
            TieringMode = tieringMode,
        };

    /// <summary>Price amount in the smallest currency unit (e.g., cents).</summary>
    public decimal Amount { get; private set; }

    /// <summary>ISO 4217 currency code (e.g., "EUR", "USD").</summary>
    public string Currency { get; private set; } = string.Empty;

    /// <summary>Billing interval this price applies to.</summary>
    public BillingInterval Interval { get; private set; }

    /// <summary>When this price version became effective.</summary>
    public DateTimeOffset EffectiveFrom { get; private set; }

    /// <summary>ID of the price version that replaced this one. Null if this is the current version.</summary>
    public Guid? ReplacedByPriceId { get; private set; }

    /// <summary>When this price was replaced by a newer version. Null if still current.</summary>
    public DateTimeOffset? ReplacedAt { get; private set; }

    /// <summary>
    /// Optional reference to a <c>Granit.Catalog.Product</c> identifier — the
    /// catalog item this price tarifs. Soft reference (no SQL FK across modules);
    /// preserved across price versions.
    /// </summary>
    public Guid? ProductId { get; private set; }

    /// <summary>
    /// Tier semantics for this price (<see cref="TieringMode.Volume"/> or
    /// <see cref="TieringMode.Graduated"/>). Set when the parent <c>Plan</c>'s
    /// <see cref="PricingModel"/> is <see cref="PricingModel.Tiered"/> and
    /// <see cref="Tiers"/> is non-empty; <c>null</c> for flat / per-seat / per-unit
    /// price points.
    /// </summary>
    public TieringMode? TieringMode { get; private set; }

    /// <summary>
    /// Bracket sequence for tiered pricing. Empty unless this price uses
    /// <see cref="PricingModel.Tiered"/>. Storage order is insertion order — the
    /// caller and <see cref="Pricing.TieredPricingCalculator"/> sort by
    /// <see cref="PricingTier.SortOrder"/> when computing; the last tier has
    /// <c>UpToQuantity = null</c> (open-ended).
    /// </summary>
    public IReadOnlyList<PricingTier> Tiers => _tiers;

    /// <summary>
    /// Replaces the tier sequence wholesale. The caller is expected to have
    /// validated ordering and the open-ended last tier; the validator on
    /// <c>SetPlanPriceTiersRequest</c> enforces both rules at the HTTP boundary.
    /// </summary>
    /// <param name="mode">Tier semantics applied to the bracket list.</param>
    /// <param name="tiers">New tier sequence (may be empty to clear).</param>
    public void SetTiers(TieringMode mode, IEnumerable<PricingTier> tiers)
    {
        ArgumentNullException.ThrowIfNull(tiers);

        _tiers.Clear();
        _tiers.AddRange(tiers);
        TieringMode = _tiers.Count == 0 ? null : mode;
    }

    /// <summary>
    /// Whether this price is the current version in the timeline (not replaced by a newer
    /// version). Computed from <see cref="ReplacedByPriceId"/> — there is no backing column
    /// and no admin toggle; the state flips only when <see cref="MarkReplaced"/> runs.
    /// </summary>
    public bool IsCurrent => ReplacedByPriceId is null;

    /// <summary>Marks this price as replaced by a newer version.</summary>
    internal void MarkReplaced(Guid replacedByPriceId, DateTimeOffset replacedAt)
    {
        if (ReplacedByPriceId is not null)
        {
            throw new InvalidOperationException(
                $"Plan price '{Id}' has already been replaced by '{ReplacedByPriceId}'.");
        }

        ReplacedByPriceId = replacedByPriceId;
        ReplacedAt = replacedAt;
    }
}
