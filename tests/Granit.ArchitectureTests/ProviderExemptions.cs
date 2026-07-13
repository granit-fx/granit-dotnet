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
        "Granit.Notifications.Brevo",                 // empty-bodied module; manual AddGranitNotificationsBrevo() required
        "Granit.Notifications.Email.Smtp",            // empty-bodied module — trap: EmailChannelOptions.Provider defaults to "Smtp"
        "Granit.Notifications.MobilePush.GoogleFcm",  // empty-bodied module; manual AddGranitNotificationsMobilePushGoogleFcm() required
        "Granit.Notifications.Twilio",                // empty-bodied module
        "Granit.Notifications.Zulip",                 // empty-bodied module
    };

    /// <summary>Providers without a registered ActivitySource — phase 1b (#2961).</summary>
    public static readonly HashSet<string> ActivitySourcePending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Brevo",                 // no OTel spans on send
        "Granit.Notifications.Email.Smtp",            // no OTel spans on send
        "Granit.Notifications.MobilePush.GoogleFcm",  // no OTel spans on send
        "Granit.Notifications.Twilio",                // no OTel spans on send
        "Granit.Notifications.Zulip",                 // no OTel spans on send
    };

    /// <summary>Providers without a health check — phase 1b (#2961).</summary>
    public static readonly HashSet<string> HealthCheckPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.MobilePush.GoogleFcm",  // only mobile-push provider without one (AwsSns/Anh have config probes)
    };

    /// <summary>Providers still nested under a channel package — renamed/merged in phase 2 (#2960).</summary>
    public static readonly HashSet<string> PlacementPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Email.AwsSes",                          // → Granit.Notifications.AwsSes
        "Granit.Notifications.Email.AzureCommunicationServices",      // → merged into Granit.Notifications.AzureCommunicationServices
        "Granit.Notifications.Email.Scaleway",                        // → Granit.Notifications.Scaleway
        "Granit.Notifications.Email.SendGrid",                        // → Granit.Notifications.SendGrid
        "Granit.Notifications.Email.Smtp",                            // → Granit.Notifications.Smtp
        "Granit.Notifications.MobilePush.AwsSns",                     // → merged into Granit.Notifications.AwsSns
        "Granit.Notifications.MobilePush.AzureNotificationHubs",      // → Granit.Notifications.AzureNotificationHubs
        "Granit.Notifications.MobilePush.GoogleFcm",                  // → Granit.Notifications.GoogleFcm
        "Granit.Notifications.Sms.AwsSns",                            // → merged into Granit.Notifications.AwsSns
        "Granit.Notifications.Sms.AzureCommunicationServices",        // → merged into Granit.Notifications.AzureCommunicationServices
    };

    /// <summary>Providers whose options validation is a no-op — phase 1b (#2961).</summary>
    public static readonly HashSet<string> ValidationPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Email.Smtp",  // ValidateOnStart() without ValidateDataAnnotations() and no IValidateOptions
    };

    /// <summary>Providers without a SectionName assertion test — phase 1b (#2961).</summary>
    public static readonly HashSet<string> SectionNameTestPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Email.AwsSes",                      // only *OptionsValidatorTests exist
        "Granit.Notifications.Email.AzureCommunicationServices",  // only *OptionsValidatorTests exist
        "Granit.Notifications.MobilePush.AwsSns",                 // no options test asserting the section path
        "Granit.Notifications.Sms.AwsSns",                        // no options test asserting the section path
        "Granit.Notifications.Sms.AzureCommunicationServices",    // no options test asserting the section path
    };

    /// <summary>Providers whose module [DependsOn] omits direct module references — phase 1b (#2961).</summary>
    public static readonly HashSet<string> DependsOnPending = new(StringComparer.Ordinal)
    {
        "Granit.Notifications.Brevo",           // missing GranitDiagnosticsModule (direct ref via HttpServiceHealthCheckBase)
        "Granit.Notifications.Email.Scaleway",  // missing GranitDiagnosticsModule
        "Granit.Notifications.Email.SendGrid",  // missing GranitDiagnosticsModule
        "Granit.Notifications.Twilio",          // missing GranitDiagnosticsModule
        "Granit.Notifications.Zulip",           // missing GranitDiagnosticsModule
    };
}
