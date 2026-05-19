using Granit.Modularity;
using Granit.Templating.Scriban.Extensions;

namespace Granit.Templating.Scriban;

/// <summary>
/// Granit module that registers the Scriban template engine.
/// </summary>
/// <remarks>
/// Implicitly depends on <c>GranitTemplatingModule</c> via
/// <see cref="ServiceCollectionExtensions.AddGranitTemplatingWithScriban"/>.
/// <para>
/// Registers:
/// <list type="bullet">
///   <item><c>ScribanTemplateEngine</c> as <c>ITemplateEngine</c> (singleton, sandboxed)</item>
///   <item><c>NowGlobalContext</c> — exposes <c>{{ now.date }}</c>, <c>{{ now.year }}</c>, etc.</item>
///   <item><c>ExecutionContextGlobalContext</c> — exposes <c>{{ context.culture }}</c>, etc.</item>
/// </list>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitTemplatingModule))]
public sealed class GranitTemplatingScribanModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTemplatingWithScriban();
}
