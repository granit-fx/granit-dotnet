namespace Granit.Modularity;

/// <summary>
/// Contexte fourni a <see cref="GranitModule.OnApplicationInitialization"/>.
/// </summary>
public sealed class ApplicationInitializationContext(IServiceProvider serviceProvider)
{
    /// <summary>Fournisseur de services resolus (apres Build()).</summary>
    public IServiceProvider ServiceProvider { get; } = serviceProvider;
}
