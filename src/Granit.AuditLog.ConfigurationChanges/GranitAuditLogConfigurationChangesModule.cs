using Granit.AuditLog.ConfigurationChanges.Handlers;
using Granit.Events;
using Granit.Features;
using Granit.Features.Events;
using Granit.Modularity;
using Granit.Settings;
using Granit.Settings.Events;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.AuditLog.ConfigurationChanges;

/// <summary>
/// Registers audit trail handlers for Settings and Feature Flags configuration changes.
/// </summary>
/// <remarks>
/// <para>
/// When loaded, every <see cref="SettingChangedEvent"/> and
/// <see cref="FeatureOverrideChangedEvent"/> published via <see cref="ILocalEventBus"/>
/// is handled and persisted as an <see cref="Domain.AuditLogEntry"/> with
/// category <see cref="Domain.AuditLogCategory.ConfigurationChange"/>.
/// </para>
/// <para>
/// Works with any event bus provider — in-process or Wolverine-backed.
/// No Wolverine dependency.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditLogModule),
    typeof(GranitFeaturesModule),
    typeof(GranitSettingsModule))]
public sealed class GranitAuditLogConfigurationChangesModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddScoped<ILocalEventHandler<SettingChangedEvent>, SettingChangedAuditHandler>();
        context.Services.AddScoped<ILocalEventHandler<FeatureOverrideChangedEvent>, FeatureOverrideChangedAuditHandler>();
    }
}
