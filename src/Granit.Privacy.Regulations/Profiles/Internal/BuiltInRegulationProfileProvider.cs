namespace Granit.Privacy.Regulations.Profiles.Internal;

/// <summary>
/// Registers built-in regulation profiles for Tier 1 (7) and Tier 2 (7) jurisdictions.
/// Values sourced from current legislation as of March 2026.
/// </summary>
internal sealed class BuiltInRegulationProfileProvider : IRegulationProfileProvider
{
    public void Define(IRegulationProfileContext context)
    {
        RegisterTier1(context);
        RegisterTier2(context);
    }

    private static void RegisterTier1(IRegulationProfileContext context)
    {
        // ── EU GDPR ────────────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.EuGdpr,
            DisplayName = "EU General Data Protection Regulation",
            JurisdictionCode = "EU",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest, LegalBasis.LegitimateInterest,
            ],
            SubjectAccessRequestDays = 30,
            SubjectAccessRequestExtensionDays = 60,
            DeletionRequestDays = 30,
            RectificationRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 90,
            ReminderDaysBefore = 3,
            BreachNotifyAuthorityHours = 72,
            MinimumConsentAge = 16,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Adequacy", "SCC", "BCR", "EU-US DPF"],
            RequiresDpoOrRepresentative = true,
            DpoNotes = "Required for public authorities, large-scale monitoring, or core processing of special categories.",
        });

        // ── UK GDPR ────────────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.UkGdpr,
            DisplayName = "United Kingdom General Data Protection Regulation",
            JurisdictionCode = "GB",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest, LegalBasis.LegitimateInterest,
            ],
            SubjectAccessRequestDays = 30,
            SubjectAccessRequestExtensionDays = 60,
            DeletionRequestDays = 30,
            RectificationRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 90,
            ReminderDaysBefore = 3,
            BreachNotifyAuthorityHours = 72,
            BreachSeverityNotes = "ICO notification required when breach likely results in risk to individuals.",
            MinimumConsentAge = 13,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Adequacy", "SCC", "BCR", "UK-US Data Bridge"],
            RequiresDpoOrRepresentative = true,
        });

        // ── Brazil LGPD ────────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.BrLgpd,
            DisplayName = "Lei Geral de Proteção de Dados",
            JurisdictionCode = "BR",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest, LegalBasis.LegitimateInterest,
                LegalBasis.CreditProtection, LegalBasis.HealthProtection,
                LegalBasis.ResearchByStudyBodies, LegalBasis.LifeProtection,
            ],
            SubjectAccessRequestDays = 15,
            DeletionRequestDays = 15,
            RectificationRequestDays = 15,
            DefaultDeletionGracePeriodDays = 15,
            MaxDeletionGracePeriodDays = 30,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "ANPD requires 'prompt' notification; timeline defined per incident.",
            MinimumConsentAge = 18,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["ANPD-approved SCC", "Adequacy"],
            RequiresDpoOrRepresentative = true,
            DpoNotes = "DPO mandatory for all controllers (except micro-enterprises). Foreign companies must appoint a local representative.",
        });

        // ── USA CCPA/CPRA ──────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.UsCcpa,
            DisplayName = "California Consumer Privacy Act / California Privacy Rights Act",
            JurisdictionCode = "US-CA",
            ConsentModel = ConsentModel.OptOut,
            AvailableLegalBases = [], // CCPA uses a consumer rights model, not legal bases
            SubjectAccessRequestDays = 45,
            SubjectAccessRequestExtensionDays = 45,
            DeletionRequestDays = 45,
            RectificationRequestDays = 45,
            DefaultDeletionGracePeriodDays = 45,
            MaxDeletionGracePeriodDays = 90,
            ReminderDaysBefore = 5,
            BreachSeverityNotes = "Notification 'without unreasonable delay'. AG notification if 500+ CA residents affected.",
            MinimumConsentAge = 16,
            CookieConsentModel = ConsentModel.OptOut,
            HonorGlobalPrivacyControl = true,
        });

        // ── Canada PIPEDA ──────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.CaPipeda,
            DisplayName = "Personal Information Protection and Electronic Documents Act",
            JurisdictionCode = "CA",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.LegalObligation,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "Notification required if 'real risk of significant harm' (RROSH).",
            MinimumConsentAge = 13,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Contractual clauses"],
        });

        // ── Canada Quebec Law 25 ───────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.CaQuebec25,
            DisplayName = "Quebec Act respecting the protection of personal information (Law 25)",
            JurisdictionCode = "CA-QC",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.LegalObligation,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "Mandatory notification for incidents presenting a serious risk of injury.",
            MinimumConsentAge = 14,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Privacy impact assessment", "Contractual clauses"],
            RequiresDpoOrRepresentative = true,
            DpoNotes = "Privacy officer mandatory.",
        });

        // ── Switzerland nFADP ──────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.ChNfadp,
            DisplayName = "Swiss Federal Act on Data Protection (nFADP)",
            JurisdictionCode = "CH",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest, LegalBasis.LegitimateInterest,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            RectificationRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 90,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "Notification to FDPIC 'as soon as possible' when breach likely poses high risk.",
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Adequacy", "SCC", "BCR"],
        });
    }

    private static void RegisterTier2(IRegulationProfileContext context)
    {
        // ── China PIPL ─────────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.CnPipl,
            DisplayName = "Personal Information Protection Law",
            JurisdictionCode = "CN",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest,
                LegalBasis.HumanResourceManagement, LegalBasis.StatutoryDuty,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachNotifyAuthorityHours = 24,
            BreachSeverityNotes = "Critical infrastructure operators: 1h. Others: 24h to CAC.",
            MinimumConsentAge = 14,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["CAC security assessment", "Certification", "Standard contract"],
            DataLocalizationRequired = true,
            DataLocalizationNotes = "Critical information infrastructure operators must store data in China.",
        });

        // ── India DPDPA ────────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.InDpdpa,
            DisplayName = "Digital Personal Data Protection Act",
            JurisdictionCode = "IN",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "Timeline to be defined by rules. Currently 'reasonable period'.",
            MinimumConsentAge = 18,
            RequiresParentalIdentityVerification = true,
            CookieConsentModel = ConsentModel.None,
        });

        // ── Japan APPI ─────────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.JpAppi,
            DisplayName = "Act on the Protection of Personal Information",
            JurisdictionCode = "JP",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.LegalObligation, LegalBasis.PublicInterest,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "Notification to PPC and affected individuals required for qualifying breaches.",
            CookieConsentModel = ConsentModel.None,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Consent", "EU adequacy"],
        });

        // ── South Korea PIPA ───────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.KrPipa,
            DisplayName = "Personal Information Protection Act",
            JurisdictionCode = "KR",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
            ],
            SubjectAccessRequestDays = 10,
            DeletionRequestDays = 10,
            DefaultDeletionGracePeriodDays = 10,
            MaxDeletionGracePeriodDays = 30,
            ReminderDaysBefore = 2,
            BreachNotifyAuthorityHours = 72,
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Consent", "EU adequacy"],
        });

        // ── Australia Privacy Act ──────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.AuPrivacyAct,
            DisplayName = "Privacy Act 1988 (reformed)",
            JurisdictionCode = "AU",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachNotifyAuthorityHours = 72,
            BreachSeverityNotes = "72h to OAIC for eligible data breaches likely to cause serious harm.",
            CookieConsentModel = ConsentModel.None,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Reasonable steps assessment"],
        });

        // ── South Africa POPIA ─────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.ZaPopia,
            DisplayName = "Protection of Personal Information Act",
            JurisdictionCode = "ZA",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.LegitimateInterest, LegalBasis.PublicInterest,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachSeverityNotes = "Notification to Information Regulator 'as soon as reasonably possible'.",
            CookieConsentModel = ConsentModel.None,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Adequate protection", "Consent", "Contract"],
            RequiresDpoOrRepresentative = true,
            DpoNotes = "Information Officer required for all responsible parties.",
        });

        // ── Thailand PDPA ──────────────────────────────────────────────
        context.Register(new PrivacyRegulationProfile
        {
            Regulation = PrivacyRegulation.ThPdpa,
            DisplayName = "Personal Data Protection Act",
            JurisdictionCode = "TH",
            ConsentModel = ConsentModel.OptIn,
            AvailableLegalBases =
            [
                LegalBasis.Consent, LegalBasis.Contract, LegalBasis.LegalObligation,
                LegalBasis.VitalInterest, LegalBasis.PublicInterest, LegalBasis.LegitimateInterest,
            ],
            SubjectAccessRequestDays = 30,
            DeletionRequestDays = 30,
            DefaultDeletionGracePeriodDays = 30,
            MaxDeletionGracePeriodDays = 60,
            ReminderDaysBefore = 3,
            BreachNotifyAuthorityHours = 72,
            BreachSeverityNotes = "72h notification to PDPC for breaches likely to cause serious damage.",
            CookieConsentModel = ConsentModel.OptIn,
            RequiresCrossBorderAssessment = true,
            TransferMechanisms = ["Adequate protection", "Consent", "Contract"],
            RequiresDpoOrRepresentative = true,
        });
    }
}
