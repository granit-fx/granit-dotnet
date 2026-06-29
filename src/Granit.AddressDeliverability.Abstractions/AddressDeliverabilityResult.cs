using Granit.Domain.ValueObjects;

namespace Granit.AddressDeliverability;

/// <summary>
/// The result of an address-deliverability lookup: the provider's <see cref="AddressDeliverabilityOutcome"/>,
/// the standardized address (when the provider corrected it), provider classification flags and the raw match code.
/// </summary>
/// <param name="Outcome">The provider's verdict.</param>
/// <param name="Standardized">The corrected / standardized address, or <c>null</c> when unchanged or unavailable.</param>
/// <param name="Flags">Provider-specific classification flags (e.g. DPV, residential, vacant); <c>null</c> when none.</param>
/// <param name="ProviderMatchCode">The provider's raw match code, or <c>null</c>.</param>
public sealed record AddressDeliverabilityResult(
    AddressDeliverabilityOutcome Outcome,
    Address? Standardized = null,
    IReadOnlyList<string>? Flags = null,
    string? ProviderMatchCode = null);
