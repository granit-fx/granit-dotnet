namespace Granit.Tax.Endpoints.Dtos;

/// <summary>Request to validate a tax ID online.</summary>
/// <param name="TaxId">The tax identification number (e.g., "BE0123456789").</param>
/// <param name="CountryCode">ISO 3166-1 alpha-2 country code.</param>
public sealed record TaxValidateRequest(string TaxId, string CountryCode);
