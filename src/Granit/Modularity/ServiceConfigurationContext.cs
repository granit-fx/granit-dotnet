using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Granit.Modularity;

/// <summary>
/// Contexte fourni a <see cref="GranitModule.ConfigureServices"/>.
/// </summary>
public sealed class ServiceConfigurationContext(
    IServiceCollection services,
    IConfiguration configuration,
    IHostApplicationBuilder builder,
    IReadOnlyList<Assembly>? moduleAssemblies = null)
{
    /// <summary>Collection de services pour l'enregistrement DI.</summary>
    public IServiceCollection Services { get; } = services;

    /// <summary>Configuration de l'application (appsettings, variables d'environnement).</summary>
    public IConfiguration Configuration { get; } = configuration;

    /// <summary>
    /// Builder complet de l'application. Necessaire pour certains modules
    /// (ex: Observability utilise <c>builder.Host.UseSerilog()</c>).
    /// </summary>
    public IHostApplicationBuilder Builder { get; } = builder;

    /// <summary>
    /// Assemblies of all loaded Granit modules (in topological order, deduplicated).
    /// Available for convention-based scanning (e.g. auto-discovering validators).
    /// </summary>
    public IReadOnlyList<Assembly> ModuleAssemblies { get; } = moduleAssemblies ?? [];

    /// <summary>
    /// Dictionnaire d'etat partage pour la communication inter-modules
    /// pendant la phase ConfigureServices.
    /// </summary>
    public IDictionary<string, object?> Items { get; } = new Dictionary<string, object?>();
}
