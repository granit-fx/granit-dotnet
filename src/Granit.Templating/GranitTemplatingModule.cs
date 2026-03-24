using Granit.Modularity;
using Granit.Templating.Extensions;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;

namespace Granit.Templating;

/// <summary>
/// Granit module for generic text templating.
/// </summary>
/// <remarks>
/// Registers the core pipeline infrastructure: <c>ITextTemplateRenderer</c>.
/// <para>
/// This module provides <strong>no</strong> template engine or resolver by default.
/// Add at least:
/// <list type="bullet">
///   <item><c>GranitTemplatingScribanModule</c> from <c>Granit.Templating.Scriban</c> for the Scriban engine.</item>
///   <item><see cref="ServiceCollectionExtensions.AddEmbeddedTemplates"/> and/or
///     <c>GranitTemplatingEntityFrameworkCoreModule</c> for template resolution.</item>
/// </list>
/// </para>
/// </remarks>
[DependsOn(typeof(GranitTimingModule))]
public sealed class GranitTemplatingModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTemplating();
}
