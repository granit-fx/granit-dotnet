namespace Granit.Privacy.Settings;

/// <summary>
/// Well-known setting names for the Granit.Privacy module.
/// </summary>
/// <remarks>
/// These settings are registered via <c>PrivacySettingDefinitionProvider</c> (auto-discovered
/// by <c>GranitSettingsModule</c>) and can be overridden per-tenant at runtime via the
/// <c>Granit.Settings</c> admin API. They satisfy GDPR Art. 13 §1(a)/(b)/(d) transparency
/// requirements (controller identity, DPO contact, supervisory authority).
/// </remarks>
#pragma warning disable GRSEC003 // Setting name constants, not secrets
public static class PrivacySettingNames
{
    /// <summary>
    /// Name of the data controller (organization or natural person responsible for the processing).
    /// GDPR Art. 13 §1(a) — mandatory.
    /// </summary>
    public const string ControllerName = "Granit.Privacy.Controller.Name";

    /// <summary>
    /// Party email of the data controller. GDPR Art. 13 §1(a) — mandatory.
    /// </summary>
    public const string ControllerEmail = "Granit.Privacy.Controller.Email";

    /// <summary>
    /// Postal address of the data controller. Recommended for full Art. 13 §1(a) compliance.
    /// </summary>
    public const string ControllerPostalAddress = "Granit.Privacy.Controller.PostalAddress";

    /// <summary>
    /// Name of the Data Protection Officer (DPO). Optional — required only when GDPR Art. 37 §1
    /// applies (public authorities, large-scale monitoring, large-scale special-category processing).
    /// </summary>
    public const string DpoName = "Granit.Privacy.Dpo.Name";

    /// <summary>
    /// Party email of the Data Protection Officer (DPO). Required when <see cref="DpoName"/>
    /// is set — Art. 13 §1(b) mandates the DPO contact be communicated to data subjects.
    /// </summary>
    public const string DpoEmail = "Granit.Privacy.Dpo.Email";

    /// <summary>
    /// URL of the competent supervisory authority (e.g. CNIL, APD, ICO). Communicated to data
    /// subjects so they can exercise their right to lodge a complaint (GDPR Art. 13 §2(d), Art. 77).
    /// </summary>
    public const string SupervisoryAuthorityUrl = "Granit.Privacy.SupervisoryAuthority.Url";
}
#pragma warning restore GRSEC003
