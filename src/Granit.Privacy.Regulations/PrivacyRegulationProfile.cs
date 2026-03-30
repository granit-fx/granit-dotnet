namespace Granit.Privacy.Regulations;

/// <summary>
/// Immutable policy object describing all jurisdiction-specific rules for a single privacy regulation.
/// Built at startup by <see cref="IRegulationProfileProvider"/> implementations,
/// resolved at runtime by <see cref="IPrivacyRegulationResolver"/>.
/// </summary>
public sealed record PrivacyRegulationProfile
{
    /// <summary>Regulation identifier (e.g., <see cref="PrivacyRegulation.EuGdpr"/>).</summary>
    public required PrivacyRegulation Regulation { get; init; }

    /// <summary>Human-readable regulation name (e.g., "EU General Data Protection Regulation").</summary>
    public required string DisplayName { get; init; }

    /// <summary>Jurisdiction code — ISO 3166-1 for countries, ISO 3166-2 for subdivisions (e.g., "EU", "BR", "US-CA", "CA-QC").</summary>
    public required string JurisdictionCode { get; init; }

    // ── Consent ────────────────────────────────────────────────────────

    /// <summary>Default consent model for this jurisdiction.</summary>
    public required ConsentModel ConsentModel { get; init; }

    /// <summary>Available legal bases for processing under this regulation.</summary>
    public required IReadOnlyList<LegalBasis> AvailableLegalBases { get; init; }

    // ── Response timelines (calendar days) ─────────────────────────────

    /// <summary>Data subject access request deadline in calendar days.</summary>
    public required int SubjectAccessRequestDays { get; init; }

    /// <summary>Maximum extension to the SAR deadline in calendar days. Null if not extendable.</summary>
    public int? SubjectAccessRequestExtensionDays { get; init; }

    /// <summary>Deletion request deadline in calendar days. Null means "prompt" or "reasonable".</summary>
    public int? DeletionRequestDays { get; init; }

    /// <summary>Rectification request deadline in calendar days.</summary>
    public int? RectificationRequestDays { get; init; }

    // ── Deletion ───────────────────────────────────────────────────────

    /// <summary>Default grace period for deferred deletion in calendar days.</summary>
    public required int DefaultDeletionGracePeriodDays { get; init; }

    /// <summary>Maximum allowed grace period for deferred deletion in calendar days.</summary>
    public required int MaxDeletionGracePeriodDays { get; init; }

    /// <summary>Number of days before the deletion deadline to send a reminder. 0 disables reminders.</summary>
    public int ReminderDaysBefore { get; init; } = 3;

    // ── Breach notification ────────────────────────────────────────────

    /// <summary>Hours to notify the supervisory authority after breach discovery. Null if unspecified.</summary>
    public int? BreachNotifyAuthorityHours { get; init; }

    /// <summary>Hours to notify affected individuals after breach discovery. Null if risk-dependent or unspecified.</summary>
    public int? BreachNotifyIndividualsHours { get; init; }

    /// <summary>Additional notes on breach severity thresholds (e.g., "PIPL: critical infra = 1h").</summary>
    public string? BreachSeverityNotes { get; init; }

    // ── Age verification ───────────────────────────────────────────────

    /// <summary>Minimum age for independent consent. 0 means no age restriction.</summary>
    public int MinimumConsentAge { get; init; }

    /// <summary>Whether parental consent requires identity verification (strictest: India DPDPA).</summary>
    public bool RequiresParentalIdentityVerification { get; init; }

    // ── Cookie consent ─────────────────────────────────────────────────

    /// <summary>Cookie consent model for this jurisdiction.</summary>
    public required ConsentModel CookieConsentModel { get; init; }

    /// <summary>Whether the Global Privacy Control (Sec-GPC) header must be honored.</summary>
    public bool HonorGlobalPrivacyControl { get; init; }

    // ── Cross-border transfers ─────────────────────────────────────────

    /// <summary>Whether cross-border data transfers require an assessment.</summary>
    public bool RequiresCrossBorderAssessment { get; init; }

    /// <summary>Available transfer mechanisms (e.g., "SCC", "BCR", "Adequacy", "CAC").</summary>
    public IReadOnlyList<string> TransferMechanisms { get; init; } = [];

    // ── Data localization ──────────────────────────────────────────────

    /// <summary>Whether data must be stored in-country.</summary>
    public bool DataLocalizationRequired { get; init; }

    /// <summary>Sector-specific localization notes.</summary>
    public string? DataLocalizationNotes { get; init; }

    // ── DPO / Representative ───────────────────────────────────────────

    /// <summary>Whether a DPO or local representative is required.</summary>
    public bool RequiresDpoOrRepresentative { get; init; }

    /// <summary>Notes on DPO or representative requirements.</summary>
    public string? DpoNotes { get; init; }

    // ── Export ──────────────────────────────────────────────────────────

    /// <summary>Required export format(s) (e.g., "JSON", "CSV", "XML").</summary>
    public IReadOnlyList<string> RequiredExportFormats { get; init; } = ["JSON"];
}
