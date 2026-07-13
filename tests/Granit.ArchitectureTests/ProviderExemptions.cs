namespace Granit.ArchitectureTests;

/// <summary>
/// Grandfathered violations of the notification-provider template
/// (<see cref="ProviderConventionTests"/>). Every entry MUST carry an inline justification
/// and its removal issue. A companion fact fails when an entry stops being necessary, so
/// this list can only shrink (honest-backlog pattern, cf. ValidationKeyConventionTests).
/// </summary>
internal static class ProviderExemptions
{
    /// <summary>Provider modules not yet self-registering in <c>ConfigureServices</c> — phase 1b (#2961).</summary>
    public static readonly HashSet<string> SelfRegistrationPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Brevo",      // empty-bodied module; manual AddGranitNotificationsBrevo() required
        "Granit.Notifications.GoogleFcm",  // empty-bodied module; manual AddGranitNotificationsGoogleFcm() required
        "Granit.Notifications.Smtp",       // empty-bodied module — trap: EmailChannelOptions.Provider defaults to "Smtp"
        "Granit.Notifications.Twilio",     // empty-bodied module
        "Granit.Notifications.Zulip",      // empty-bodied module
    };

    /// <summary>Providers without a registered ActivitySource — phase 1b (#2961).</summary>
    public static readonly HashSet<string> ActivitySourcePending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Brevo",      // no OTel spans on send
        "Granit.Notifications.GoogleFcm",  // no OTel spans on send
        "Granit.Notifications.Smtp",       // no OTel spans on send
        "Granit.Notifications.Twilio",     // no OTel spans on send
        "Granit.Notifications.Zulip",      // no OTel spans on send
    };

    /// <summary>Providers without a health check — phase 1b (#2961).</summary>
    public static readonly HashSet<string> HealthCheckPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.GoogleFcm",  // only mobile-push provider without one (AwsSns/AzureNotificationHubs have config probes)
    };

    /// <summary>
    /// Providers nested under a channel package. Emptied by phase 2 (#2960) — the placement
    /// rule is now fully enforced; new providers must be top-level from day one.
    /// </summary>
    public static readonly HashSet<string> PlacementPending = new(StringComparer.Ordinal);

    /// <summary>Providers whose options validation is a no-op — phase 1b (#2961).</summary>
    public static readonly HashSet<string> ValidationPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Smtp",  // ValidateOnStart() without ValidateDataAnnotations() and no IValidateOptions
    };

    /// <summary>Providers without a SectionName assertion test — phase 1b (#2961).</summary>
    public static readonly HashSet<string> SectionNameTestPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.AwsSes",                      // only *OptionsValidatorTests exist
        "Granit.Notifications.AwsSns",                      // merged test project has no SectionName assertions yet
        "Granit.Notifications.AzureCommunicationServices",  // merged test project has no SectionName assertions yet
    };

    /// <summary>Providers whose module [DependsOn] omits direct module references — phase 1b (#2961).</summary>
    public static readonly HashSet<string> DependsOnPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Brevo",     // missing GranitDiagnosticsModule (direct ref via HttpServiceHealthCheckBase)
        "Granit.Notifications.Scaleway",  // missing GranitDiagnosticsModule
        "Granit.Notifications.SendGrid",  // missing GranitDiagnosticsModule
        "Granit.Notifications.Twilio",    // missing GranitDiagnosticsModule
        "Granit.Notifications.Zulip",     // missing GranitDiagnosticsModule
    };
}
