using System.Reflection;
using Granit.Caching;
using Granit.Encryption;
using Granit.Events;
using Granit.Modularity;
using Granit.Settings.Definitions;
using Granit.Settings.Extensions;
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
