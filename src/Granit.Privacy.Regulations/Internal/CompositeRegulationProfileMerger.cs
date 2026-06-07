namespace Granit.Privacy.Regulations.Internal;

/// <summary>
/// Merges multiple <see cref="PrivacyRegulationProfile"/> instances into a single
/// composite effective profile using deterministic "most restrictive wins" rules.
/// </summary>
/// <remarks>
/// Merge rules applied per field:
/// <list type="bullet">
///   <item>Consent model — lowest enum value wins (<see cref="ConsentModel.OptIn"/> = 0 is strictest).</item>
///   <item>Response SLA — minimum calendar days; null ("prompt/reasonable") treated as unbounded.</item>
///   <item>Breach notification deadlines — minimum hours; null ignored (use other's value).</item>
///   <item>Legal bases — union of all contributing profiles.</item>
///   <item>Export formats — union of all contributing profiles.</item>
///   <item>Transfer mechanisms — union (each profile's available mechanisms listed).</item>
///   <item>Boolean safety flags — OR (true if any profile requires it).</item>
///   <item>Minimum consent age — maximum (most protective).</item>
/// </list>
/// Passing a single-element list returns that profile unchanged.
/// </remarks>
internal static class CompositeRegulationProfileMerger
{
    /// <summary>
    /// Merges <paramref name="profiles"/> into a single composite profile.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown when <paramref name="profiles"/> is empty.</exception>
    public static PrivacyRegulationProfile Merge(IReadOnlyList<PrivacyRegulationProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        if (profiles.Count == 0)
        {
            throw new ArgumentException("At least one profile is required for merging.", nameof(profiles));
        }

        if (profiles.Count == 1)
        {
            return profiles[0];
        }

        string compositeCode = string.Join("+", profiles.Select(p => p.Regulation.Value));
        string compositeDisplay = string.Join(", ", profiles.Select(p => p.DisplayName));
        string compositeJurisdiction = string.Join("+", profiles.Select(p => p.JurisdictionCode));

        return new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.Create(compositeCode),
            DisplayName = compositeDisplay,
            JurisdictionCode = compositeJurisdiction,

            // Consent — strictest (lowest enum value)
            ConsentModel = profiles.Min(p => p.ConsentModel),
            CookieConsentModel = profiles.Min(p => p.CookieConsentModel),

            // Legal bases — union
            AvailableLegalBases = profiles
                .SelectMany(p => p.AvailableLegalBases)
                .Distinct()
                .ToList(),

            // Response timelines — minimum (null = unbounded, skipped in min)
            SubjectAccessRequestDays = profiles.Min(p => p.SubjectAccessRequestDays),
            SubjectAccessRequestExtensionDays = MinNullable(profiles.Select(p => p.SubjectAccessRequestExtensionDays)),
            DeletionRequestDays = MinNullable(profiles.Select(p => p.DeletionRequestDays)),
            RectificationRequestDays = MinNullable(profiles.Select(p => p.RectificationRequestDays)),

            // Deletion grace period — minimum
            DefaultDeletionGracePeriodDays = profiles.Min(p => p.DefaultDeletionGracePeriodDays),
            MaxDeletionGracePeriodDays = profiles.Min(p => p.MaxDeletionGracePeriodDays),
            ReminderDaysBefore = profiles.Min(p => p.ReminderDaysBefore),

            // Breach notification — minimum non-null deadline
            BreachNotifyAuthorityHours = MinNullable(profiles.Select(p => p.BreachNotifyAuthorityHours)),
            BreachNotifyIndividualsHours = MinNullable(profiles.Select(p => p.BreachNotifyIndividualsHours)),
            BreachSeverityNotes = CombineNotes(profiles.Select(p => p.BreachSeverityNotes)),

            // Age verification — strictest
            MinimumConsentAge = profiles.Max(p => p.MinimumConsentAge),
            RequiresParentalIdentityVerification = profiles.Any(p => p.RequiresParentalIdentityVerification),

            // Cookie & GPC
            HonorGlobalPrivacyControl = profiles.Any(p => p.HonorGlobalPrivacyControl),

            // Cross-border — most restrictive
            RequiresCrossBorderAssessment = profiles.Any(p => p.RequiresCrossBorderAssessment),
            TransferMechanisms = profiles.SelectMany(p => p.TransferMechanisms).Distinct().ToList(),

            // Data localization — most restrictive
            DataLocalizationRequired = profiles.Any(p => p.DataLocalizationRequired),
            DataLocalizationNotes = CombineNotes(profiles.Select(p => p.DataLocalizationNotes)),

            // DPO — most restrictive
            RequiresDpoOrRepresentative = profiles.Any(p => p.RequiresDpoOrRepresentative),
            DpoNotes = CombineNotes(profiles.Select(p => p.DpoNotes)),

            // Export formats — union
            RequiredExportFormats = profiles.SelectMany(p => p.RequiredExportFormats).Distinct().ToList(),
        };
    }

    private static int? MinNullable(IEnumerable<int?> values)
    {
        int? result = null;
        foreach (int? v in values)
        {
            if (v.HasValue && (result is null || v.Value < result.Value))
            {
                result = v;
            }
        }
        return result;
    }

    private static string? CombineNotes(IEnumerable<string?> notes)
    {
        string combined = string.Join(" | ", notes.Where(n => !string.IsNullOrWhiteSpace(n))!);
        return string.IsNullOrEmpty(combined) ? null : combined;
    }
}
