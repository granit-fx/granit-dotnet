namespace Granit.Tax.Endpoints.Dtos;

/// <summary>Tax rate for a country.</summary>
public sealed record TaxRateResponse(
    string CountryCode,
    decimal StandardRate,
    decimal? ReducedRate,
    decimal? SuperReducedRate,
    decimal? ParkingRate,
    DateTimeOffset? EffectiveFrom,
    DateTimeOffset? EffectiveTo);
