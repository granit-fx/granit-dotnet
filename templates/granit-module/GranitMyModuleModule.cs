using Granit.Modularity;

namespace Granit.MyModule;

/// <summary>
/// Granit module for MyModule.
/// </summary>
public sealed class GranitMyModuleModule : GranitModule
{
    /// <inheritdoc />
    public override void ConfigureServices(ServiceConfigurationContext context)
    {
        // Register your services here.
        // Example: context.Services.AddScoped<IMyService, MyService>();
    }
}
