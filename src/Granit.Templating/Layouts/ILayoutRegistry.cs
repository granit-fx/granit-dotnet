namespace Granit.Templating.Layouts;

/// <summary>
/// Resolves the default layout template name for a given content template.
/// </summary>
/// <remarks>
/// Populated at DI registration time via
/// <see cref="Extensions.ServiceCollectionExtensions.AddTemplateLayout"/>.
/// Code-level defaults — can be overridden per-template via <c>LayoutName</c>
/// on <see cref="Pipeline.TemplateDescriptor"/>.
/// </remarks>
public interface ILayoutRegistry
{
    /// <summary>
    /// Returns the layout template name for the given content template, or <c>null</c>
    /// if no layout is registered (template renders standalone).
    /// </summary>
    /// <param name="templateName">The content template name (e.g. <c>"Security.Welcome"</c>).</param>
    /// <returns>
    /// Layout template name (e.g. <c>"Layout.Email"</c>) or <c>null</c>.
    /// </returns>
    string? GetLayoutName(string templateName);

    /// <summary>
    /// Returns all distinct layout template names registered in the registry.
    /// Used by admin endpoints to populate layout dropdowns.
    /// </summary>
    IReadOnlyList<string> GetAllLayoutNames();
}
