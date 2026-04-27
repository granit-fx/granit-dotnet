using Granit.Settings.Definitions;

namespace Granit.Privacy.Settings;

/// <summary>
/// Declares the privacy contact settings (controller, DPO, supervisory authority).
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitSettingsModule</c> — no manual registration needed.
/// All values are visible to clients (front-ends consume them on the privacy policy page,
/// notification templates inject them into emails). Tenant overrides take precedence over
/// the global defaults; suitable for multi-tenant SaaS where each tenant has its own
/// data controller and DPO.
/// </remarks>
internal sealed class PrivacySettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <inheritdoc />
    public void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(PrivacySettingNames.ControllerName)
        {
            IsVisibleToClients = true,
            DisplayName = "Data controller — name",
            Description = "Name of the organization or natural person responsible for the processing of personal data (GDPR Art. 13 §1(a)).",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(PrivacySettingNames.ControllerEmail)
        {
            IsVisibleToClients = true,
            DisplayName = "Data controller — contact email",
            Description = "Contact email address of the data controller (GDPR Art. 13 §1(a)).",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(PrivacySettingNames.ControllerPostalAddress)
        {
            IsVisibleToClients = true,
            DisplayName = "Data controller — postal address",
            Description = "Postal address of the data controller (recommended for full GDPR Art. 13 §1(a) compliance).",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(PrivacySettingNames.DpoName)
        {
            IsVisibleToClients = true,
            DisplayName = "Data Protection Officer — name",
            Description = "Name of the Data Protection Officer. Optional — required only when GDPR Art. 37 §1 applies.",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(PrivacySettingNames.DpoEmail)
        {
            IsVisibleToClients = true,
            DisplayName = "Data Protection Officer — contact email",
            Description = "Contact email of the Data Protection Officer (GDPR Art. 13 §1(b), Art. 38 §4).",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(PrivacySettingNames.SupervisoryAuthorityUrl)
        {
            IsVisibleToClients = true,
            DisplayName = "Supervisory authority — URL",
            Description = "URL of the competent supervisory authority (e.g. CNIL, APD, ICO) where data subjects can lodge a complaint (GDPR Art. 13 §2(d), Art. 77).",
            Providers = { "T", "G" },
        });
    }
}
