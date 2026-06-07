namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Response returning the effective privacy regulation profile for the current tenant.
/// When multiple jurisdictions are declared, the profile represents the merged composite
/// (most restrictive rules win). <see cref="ContributingRegulations"/> lists the source regulations.
/// </summary>
public sealed record PrivacyRegulationProfileResponse(
    string Regulation,
    string DisplayName,
    string JurisdictionCode,
    IReadOnlyList<string> ContributingRegulations,
    string ConsentModel,
    IReadOnlyList<string> AvailableLegalBases,
    int SubjectAccessRequestDays,
    int? SubjectAccessRequestExtensionDays,
    int? DeletionRequestDays,
    int DefaultDeletionGracePeriodDays,
    int MaxDeletionGracePeriodDays,
    int? BreachNotifyAuthorityHours,
    int? BreachNotifyIndividualsHours,
    int MinimumConsentAge,
    bool RequiresParentalIdentityVerification,
    string CookieConsentModel,
    bool HonorGlobalPrivacyControl,
    bool RequiresCrossBorderAssessment,
    IReadOnlyList<string> TransferMechanisms,
    bool DataLocalizationRequired,
    bool RequiresDpoOrRepresentative,
    IReadOnlyList<string> RequiredExportFormats);
