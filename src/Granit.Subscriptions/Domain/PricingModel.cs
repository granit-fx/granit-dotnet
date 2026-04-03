namespace Granit.Subscriptions.Domain;

/// <summary>
/// Pricing model for a subscription plan.
/// </summary>
public enum PricingModel
{
    /// <summary>Fixed price per billing interval regardless of usage or seats.</summary>
    Flat = 0,

    /// <summary>Price per assigned seat (user) per billing interval.</summary>
    PerSeat = 1,

    /// <summary>Price per unit of consumption per billing interval.</summary>
    PerUnit = 2,

    /// <summary>Tiered pricing with volume-based price breaks.</summary>
    Tiered = 3,
}
