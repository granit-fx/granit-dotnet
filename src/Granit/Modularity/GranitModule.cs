namespace Granit.Modularity;

/// <summary>
/// Base class for all Granit modules.
/// Override lifecycle methods (sync or async) to register
/// services or configure the application pipeline.
/// </summary>
public abstract class GranitModule
{
    /// <summary>
    /// Determines whether this module is enabled and should execute its lifecycle methods.
    /// Disabled modules remain in the dependency graph but their
    /// <see cref="ConfigureServices"/> and <see cref="OnApplicationInitialization"/>
    /// methods are skipped.
    /// </summary>
    /// <param name="context">Service configuration context (provides <c>Configuration</c>).</param>
    /// <returns><c>true</c> if the module should be loaded; <c>false</c> to skip.</returns>
    public virtual bool IsEnabled(ServiceConfigurationContext context) => true;

    /// <summary>
    /// Registers the module's services in the DI container (synchronous version).
    /// Called in topological order (dependencies first).
    /// </summary>
    public virtual void ConfigureServices(ServiceConfigurationContext context)
    {
    }

    /// <summary>
    /// Registers the module's services in the DI container (asynchronous version).
    /// By default, calls <see cref="ConfigureServices"/>.
    /// Override this method for modules requiring async configuration
    /// (e.g. reading remote secrets, verifying connectivity).
    /// </summary>
    public virtual Task ConfigureServicesAsync(ServiceConfigurationContext context)
    {
        ConfigureServices(context);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Initializes the module after the application is built (synchronous version).
    /// Called after <c>builder.Build()</c>, before <c>app.Run()</c>.
    /// </summary>
    public virtual void OnApplicationInitialization(ApplicationInitializationContext context)
    {
    }

    /// <summary>
    /// Initializes the module after the application is built (asynchronous version).
    /// By default, calls <see cref="OnApplicationInitialization"/>.
    /// </summary>
    public virtual Task OnApplicationInitializationAsync(ApplicationInitializationContext context)
    {
        OnApplicationInitialization(context);
        return Task.CompletedTask;
    }
}
