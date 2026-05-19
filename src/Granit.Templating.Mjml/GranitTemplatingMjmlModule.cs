using Granit.Modularity;
using Granit.Templating.Mjml.Extensions;

namespace Granit.Templating.Mjml;

/// <summary>
/// Granit module that registers the MJML template transformer.
/// </summary>
/// <remarks>
/// When this module is loaded, templates containing MJML markup are automatically compiled
/// to email-client-safe HTML (table-based layout, inline CSS, MSO conditional comments)
/// as part of the rendering pipeline.
/// <para>
/// Plain HTML templates continue to work unchanged — the transformer only processes
/// content starting with <c>&lt;mjml&gt;</c>.
/// </para>
/// </remarks>
[DependsOn(typeof(GranitTemplatingModule))]
public sealed class GranitTemplatingMjmlModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitTemplatingWithMjml();
}
