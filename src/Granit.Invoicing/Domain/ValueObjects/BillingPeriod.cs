namespace Granit.Invoicing.Domain.ValueObjects;

/// <summary>
/// Represents a billing period with start and end dates.
/// </summary>
/// <param name="Start">Start of the billing period (inclusive).</param>
/// <param name="End">End of the billing period (exclusive).</param>
public sealed record BillingPeriod(DateTimeOffset Start, DateTimeOffset End);
