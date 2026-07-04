namespace Granit.Templating.Keys;

/// <summary>
/// Identifies the target output format of a document generation pipeline.
/// </summary>
/// <remarks>
/// Carried through the pipeline from <see cref="Pipeline.ITemplateEngine.RenderAsync{TData}"/>
/// to <see cref="Pipeline.RenderedContent"/> so that downstream renderers can select
/// the correct implementation.
/// </remarks>
public enum DocumentFormat
{
    /// <summary>HTML text output — used by <c>ITextTemplateRenderer</c> for email, SMS and push notifications.</summary>
    Html,

    /// <summary>PDF binary output — requires <c>Granit.DocumentGeneration.Pdf</c>.</summary>
    Pdf,

    /// <summary>Excel binary output — requires <c>Granit.DocumentGeneration.Excel</c>.</summary>
    Excel,
}
