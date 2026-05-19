using Granit.DocumentGeneration.Extensions;
using Granit.Modularity;
using Granit.Templating;

namespace Granit.DocumentGeneration;

/// <summary>
/// Granit module for document generation (PDF, Excel, etc.).
/// </summary>
/// <remarks>
/// Registers <see cref="Pipeline.IDocumentGenerator"/> (scoped).
/// <para>
/// Also depends on <see cref="GranitTemplatingModule"/> to ensure the text rendering
/// pipeline is available (enrichers, resolvers, engine).
/// </para>
/// <para>
/// At least one <c>IDocumentRenderer</c> must be registered separately. Available packages:
/// <list type="bullet">
///   <item><c>Granit.DocumentGeneration.Pdf</c> — PuppeteerSharp headless-Chrome renderer</item>
/// </list>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitTemplatingModule))]
public sealed class GranitDocumentGenerationModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitDocumentGeneration();
}
