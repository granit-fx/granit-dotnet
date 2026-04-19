using System.Reflection;
using Granit.Caching;
using Granit.DataExchange.Extensions;
using Granit.Encryption;
using Granit.Events;
using Granit.Modularity;
using Granit.QueryEngine.Extensions;
using Granit.Settings.Definitions;
using Granit.Settings.Domain;
using Granit.Settings.Exports;
using Granit.Settings.Extensions;
using Granit.Settings.Queries;
using Granit.Users;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Settings;

/// <summary>
/// Granit module for dynamic settings management with cascading resolution.
/// Auto-discovers all <see cref="ISettingDefinitionProvider"/> implementations
/// across loaded module assemblies.
/// </summary>
[DependsOn(typeof(GranitCachingModule))]
[DependsOn(typeof(GranitEncryptionModule))]
[DependsOn(typeof(GranitEventsModule))]
public sealed class GranitSettingsModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        context.Services.AddGranitSettings();

        context.Services.AddQueryDefinition<SettingRecord, SettingRecordQueryDefinition>();
        context.Services.AddExportDefinition<SettingRecord, SettingRecordExportDefinition>();

        foreach (Assembly assembly in context.ModuleAssemblies)
        {
            IEnumerable<Type> providerTypes = assembly.GetTypes()
                .Where(t => t is { IsAbstract: false, IsInterface: false }
                    && typeof(ISettingDefinitionProvider).IsAssignableFrom(t));

            foreach (Type providerType in providerTypes)
            {
                context.Services.AddSingleton(typeof(ISettingDefinitionProvider), providerType);
            }
        }
    }
}
