using Granit.Html.AngleSharp.Extensions;
using Granit.Modularity;

namespace Granit.Html.AngleSharp;

/// <summary>
/// Granit module that registers the AngleSharp-backed
/// <see cref="IHtmlToPlainTextConverter"/> implementation. Depends on
/// <see cref="GranitHtmlModule"/> so consumers don't have to declare both.
/// </summary>
[DependsOn(typeof(GranitHtmlModule))]
public sealed class GranitHtmlAngleSharpModule : GranitModule
{
    /// <inheritdoc/>
    public override void ConfigureServices(ServiceConfigurationContext context) =>
        context.Services.AddGranitHtmlAngleSharp();
}
