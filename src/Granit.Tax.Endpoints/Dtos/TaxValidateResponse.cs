namespace Granit.Tax.Endpoints.Dtos;

/// <summary>Result of a tax ID validation.</summary>
public sealed record TaxValidateResponse(
    bool IsValid,
    string? CompanyName,
    string? CompanyAddress,
    string? RequestIdentifier,
    DateTimeOffset? ValidatedAt,
    string Source);
