namespace Granit.Notifications.WhatsApp.Options;

/// <summary>Configuration for the WhatsApp notification channel.</summary>
public sealed class WhatsAppChannelOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "Notifications:WhatsApp";

    /// <summary>Provider key for Keyed Services resolution (e.g. "Brevo", "Twilio"). Required.</summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    /// Terminal code-level BCP-47 language, used only when the whole chain is empty:
    /// trigger culture, recipient preferred culture (User setting), and the
    /// <c>Granit.Localization.PreferredCulture</c> setting (Tenant/Global levels).
    /// Replaces the previously hardcoded "fr".
    /// </summary>
    public string DefaultLanguage { get; set; } = "fr";
}
