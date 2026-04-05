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
            Description = "When enabled, new users can create an account via the public registration endpoint.",
            Providers = { "T", "G" },
        });
    }
}
