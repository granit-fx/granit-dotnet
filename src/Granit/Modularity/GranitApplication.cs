using Microsoft.Extensions.Logging;

namespace Granit.Modularity;

/// <summary>
/// Orchestrates the lifecycle of Granit modules.
/// Registered as a singleton by <c>AddGranit&lt;T&gt;()</c>
/// or <c>AddGranitAsync&lt;T&gt;()</c>.
/// </summary>
public sealed partial class GranitApplication
{
    private readonly IReadOnlyList<ModuleDescriptor> _modules;
    private readonly ILogger<GranitApplication> _logger;

    internal GranitApplication(IReadOnlyList<ModuleDescriptor> modules, ILogger<GranitApplication> logger)
    {
        _modules = modules;
        _logger = logger;
    }

    /// <summary>Returns the types of loaded modules in topological order (for diagnostics).</summary>
    public IReadOnlyList<Type> GetModuleTypes() =>
        [.. _modules.Select(m => m.ModuleType)];

    /// <summary>Returns the module instances in topological order (dependencies first).</summary>
    public IReadOnlyList<GranitModule> GetModuleInstances() =>
        [.. _modules.Where(m => m.IsEnabled).Select(m => m.Instance)];

    /// <summary>
    /// Calls <see cref="GranitModule.ConfigureServices"/> on each module
    /// in topological order (synchronous version).
    /// Modules returning <c>false</c> from <see cref="GranitModule.IsEnabled"/> are skipped.
    /// </summary>
    internal void ConfigureServices(ServiceConfigurationContext context)
    {
        LogModuleList(context);

        foreach (ModuleDescriptor module in _modules)
        {
            if (!module.Instance.IsEnabled(context))
            {
                LogModuleDisabled(module.ModuleType.Name);
                continue;
            }

            module.Instance.ConfigureServices(context);
        }
    }

    /// <summary>
    /// Calls <see cref="GranitModule.ConfigureServicesAsync"/> on each module
    /// in topological order (asynchronous version).
    /// Modules returning <c>false</c> from <see cref="GranitModule.IsEnabled"/> are skipped.
    /// </summary>
    internal async Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        LogModuleList(context);

        foreach (ModuleDescriptor module in _modules)
        {
            if (!module.Instance.IsEnabled(context))
            {
                LogModuleDisabled(module.ModuleType.Name);
                continue;
            }

            await module.Instance.ConfigureServicesAsync(context).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Calls <see cref="GranitModule.OnApplicationInitialization"/> on each module
    /// in topological order (synchronous version).
    /// </summary>
    internal void InitializeApplication(ApplicationInitializationContext context)
    {
        foreach (ModuleDescriptor module in _modules)
        {
            if (!module.IsEnabled)
            {
                continue;
            }

            module.Instance.OnApplicationInitialization(context);
        }
    }

    /// <summary>
    /// Calls <see cref="GranitModule.OnApplicationInitializationAsync"/> on each module
    /// in topological order (asynchronous version).
    /// </summary>
    internal async Task InitializeApplicationAsync(ApplicationInitializationContext context)
    {
        foreach (ModuleDescriptor module in _modules)
        {
            if (!module.IsEnabled)
            {
                continue;
            }

            await module.Instance.OnApplicationInitializationAsync(context).ConfigureAwait(false);
        }
    }

    private void LogModuleList(ServiceConfigurationContext context)
    {
        foreach (ModuleDescriptor module in _modules)
        {
            bool enabled = module.Instance.IsEnabled(context);
            module.IsEnabled = enabled;
            string status = enabled ? "OK" : "DISABLED";
            LogModuleLoaded(module.ModuleType.Name, status);
        }

        LogModuleSummary(_modules.Count, _modules.Count(m => m.IsEnabled));
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit module {ModuleName} [{Status}]")]
    private partial void LogModuleLoaded(string moduleName, string status);

    [LoggerMessage(Level = LogLevel.Information, Message = "Granit: {TotalCount} modules loaded, {EnabledCount} enabled")]
    private partial void LogModuleSummary(int totalCount, int enabledCount);

    [LoggerMessage(Level = LogLevel.Debug, Message = "Skipping disabled module {ModuleName}")]
    private partial void LogModuleDisabled(string moduleName);
}
