using Granit.AuditLog.Wolverine.Publishers;
using Granit.Core.Modularity;
using Granit.Features;
using Granit.Features.Events;
using Granit.Settings;
using Granit.Settings.Events;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Granit.AuditLog.Wolverine;

/// <summary>
/// Wolverine integration for Granit.AuditLog: replaces no-op event publishers with
/// <see cref="global::Wolverine.IMessageBus"/>-backed implementations and provides
/// handlers that persist configuration changes in the audit trail.
/// </summary>
/// <remarks>
/// <para>
/// When this module is loaded, every <see cref="SettingChangedEvent"/> and
/// <see cref="FeatureOverrideChangedEvent"/> is published to the Wolverine bus
/// and handled by <c>SettingChangedAuditHandler</c> / <c>FeatureOverrideChangedAuditHandler</c>,
/// which persist them as <c>AuditLogEntry</c> with category <c>ConfigurationChange</c>.
/// </para>
/// </remarks>
[DependsOn(
    typeof(GranitAuditLogModule),
    typeof(GranitFeaturesModule),
    typeof(GranitSettingsModule),
    typeof(GranitWolverineModule))]
public sealed class GranitAuditLogWolverineModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Replace no-op publishers with Wolverine-backed implementations.
        context.Services.Replace(ServiceDescriptor
            .Scoped<ISettingEventPublisher, WolverineSettingEventPublisher>());
        context.Services.Replace(ServiceDescriptor
            .Scoped<IFeatureEventPublisher, WolverineFeatureEventPublisher>());
    }
}
