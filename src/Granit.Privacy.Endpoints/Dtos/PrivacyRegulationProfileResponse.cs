namespace Granit.Privacy.Endpoints.Dtos;

/// <summary>
/// Response returning the privacy regulation profile applicable to the current tenant.
/// </summary>
public sealed record PrivacyRegulationProfileResponse(
    string Regulation,
    string DisplayName,
    string JurisdictionCode,
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
