using Granit.Settings.Definitions;

namespace Granit.Identity.Local.Internal;

/// <summary>
/// Declares the settings for the Identity.Local module.
/// </summary>
/// <remarks>
/// Auto-discovered by <c>GranitSettingsModule</c> — no manual registration needed.
/// </remarks>
internal sealed class IdentityLocalSettingDefinitionProvider : ISettingDefinitionProvider
{
    /// <inheritdoc />
    public void Define(ISettingDefinitionContext context)
    {
        context.Add(new SettingDefinition(IdentityLocalSettingNames.AllowSelfRegistration)
        {
            DefaultValue = "false",
            IsVisibleToClients = true,
            DisplayName = "Allow self-registration",
            Description = "Master switch for account creation. When enabled, new users can create an "
                + "account via the public registration endpoint or an external provider. When disabled, "
                + "external login authenticates existing accounts only and never creates new ones.",
            Providers = { "T", "G" },
        });

        context.Add(new SettingDefinition(IdentityLocalSettingNames.DefaultUserRole)
        {
            DefaultValue = "",
            IsVisibleToClients = false,
            DisplayName = "Default user role",
            Description = "Role assigned to every newly self-registered user (local and external). "
                + "Empty disables the behavior. The role must already exist; otherwise registration "
                + "succeeds but no role is assigned.",
            Providers = { "T", "G" },
        });
    }
}
