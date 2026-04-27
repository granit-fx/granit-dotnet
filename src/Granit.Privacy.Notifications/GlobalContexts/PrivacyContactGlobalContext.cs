using Granit.Privacy.Settings;
using Granit.Settings.Services;
using Granit.Settings.Values;
using Granit.Templating.GlobalContext;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Privacy.Notifications.GlobalContexts;

/// <summary>
/// Injects the data controller and Data Protection Officer (DPO) contact into every
/// template under the <c>privacy</c> namespace.
/// </summary>
/// <remarks>
/// <para>
/// Values come from <c>Granit.Settings</c> (cascading global → tenant). Tenant overrides
/// take precedence so that multi-tenant SaaS apps can have a per-tenant data controller
/// and DPO. Missing settings render as empty strings — templates can use Scriban's
/// <c>{{ if privacy.dpo_email }}</c> to conditionally show the DPO block.
/// </para>
/// <para>
/// Available template variables (GDPR Art. 13 §1 transparency requirements):
/// <list type="table">
///   <listheader><term>Variable</term><description>Source setting</description></listheader>
///   <item><term><c>{{ privacy.controller_name }}</c></term><description><see cref="PrivacySettingNames.ControllerName"/></description></item>
///   <item><term><c>{{ privacy.controller_email }}</c></term><description><see cref="PrivacySettingNames.ControllerEmail"/></description></item>
///   <item><term><c>{{ privacy.controller_postal_address }}</c></term><description><see cref="PrivacySettingNames.ControllerPostalAddress"/></description></item>
///   <item><term><c>{{ privacy.dpo_name }}</c></term><description><see cref="PrivacySettingNames.DpoName"/></description></item>
///   <item><term><c>{{ privacy.dpo_email }}</c></term><description><see cref="PrivacySettingNames.DpoEmail"/></description></item>
///   <item><term><c>{{ privacy.supervisory_authority_url }}</c></term><description><see cref="PrivacySettingNames.SupervisoryAuthorityUrl"/></description></item>
/// </list>
/// </para>
/// <para>
/// <strong>Performance:</strong> a single <see cref="ISettingProvider.GetAllAsync(string[], System.Threading.CancellationToken)"/>
/// batch call per render. <c>Granit.Settings</c> caches the values, so cold reads cost
/// one DB roundtrip per tenant; subsequent renders hit the cache.
/// </para>
/// </remarks>
internal sealed class PrivacyContactGlobalContext(IServiceScopeFactory scopeFactory) : ITemplateGlobalContext
{
    private static readonly string[] AllSettings =
    [
        PrivacySettingNames.ControllerName,
        PrivacySettingNames.ControllerEmail,
        PrivacySettingNames.ControllerPostalAddress,
        PrivacySettingNames.DpoName,
        PrivacySettingNames.DpoEmail,
        PrivacySettingNames.SupervisoryAuthorityUrl,
    ];

    /// <inheritdoc/>
    public string ContextName => "privacy";

    /// <inheritdoc/>
    public object Resolve()
    {
        // ISettingProvider is scoped (depends on ICurrentTenant). PrivacyContactGlobalContext
        // is a singleton, so we open a short-lived scope to resolve it. The settings cache
        // makes the per-render overhead negligible after the first hit.
        using IServiceScope scope = scopeFactory.CreateScope();
        ISettingProvider provider = scope.ServiceProvider.GetRequiredService<ISettingProvider>();

        IReadOnlyList<SettingValue> values = provider
            .GetAllAsync(AllSettings)
            .GetAwaiter()
            .GetResult();

        var map = values.ToDictionary(v => v.Name, v => v.Value);

        return new
        {
            controller_name = map.GetValueOrDefault(PrivacySettingNames.ControllerName) ?? string.Empty,
            controller_email = map.GetValueOrDefault(PrivacySettingNames.ControllerEmail) ?? string.Empty,
            controller_postal_address = map.GetValueOrDefault(PrivacySettingNames.ControllerPostalAddress) ?? string.Empty,
            dpo_name = map.GetValueOrDefault(PrivacySettingNames.DpoName) ?? string.Empty,
            dpo_email = map.GetValueOrDefault(PrivacySettingNames.DpoEmail) ?? string.Empty,
            supervisory_authority_url = map.GetValueOrDefault(PrivacySettingNames.SupervisoryAuthorityUrl) ?? string.Empty,
        };
    }
}
