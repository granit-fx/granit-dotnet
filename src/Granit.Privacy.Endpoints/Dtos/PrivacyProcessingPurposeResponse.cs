namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Response returning a registered processing purpose with its legal basis.
/// </summary>
public sealed record PrivacyProcessingPurposeResponse(
    string PurposeId,
    string DisplayName,
    string Description,
    string LegalBasis,
    bool RequiresExplicitConsent,
    string? DataCategory);
