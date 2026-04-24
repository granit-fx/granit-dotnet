namespace Granit.Catalog.Domain;

/// <summary>
/// Coarse classification of a <see cref="Product"/>.
/// Drives downstream behavior: tax codes (Avalara), shipping
/// (none for digital/service), metering integration (Metered).
/// </summary>
public enum ProductType
{
    /// <summary>
    /// A consultancy or one-off service (e.g., onboarding, migration help, training).
    /// Not measured; billed per occurrence or as a fixed fee on a subscription.
    /// </summary>
    Service = 0,

    /// <summary>
    /// A measured / metered service billed on usage
    /// (e.g., API calls, GB stored, minutes processed). Bound to a
    /// <c>Granit.Metering.MeterDefinition</c> via the meter's <c>ProductId</c>.
    /// </summary>
    Metered = 1,

    /// <summary>
    /// A physical good shipped to the customer (future e-commerce phase).
    /// </summary>
    Physical = 2,

    /// <summary>
    /// A digital good delivered electronically — e-book, license key, downloadable
    /// software (future e-commerce phase).
    /// </summary>
    Digital = 3,
}
