namespace Granit.Templating.Layouts;

/// <summary>
/// Maps a template name or prefix pattern to a layout template name.
/// Registered via <see cref="Extensions.ServiceCollectionExtensions.AddTemplateLayout"/>.
/// </summary>
/// <param name="Pattern">
/// Either an exact template name (<c>"Billing.Invoice"</c>) or a prefix
/// with wildcard (<c>"Billing.*"</c>) matching all templates in that namespace.
/// </param>
/// <param name="LayoutTemplateName">
/// The logical template name of the layout (e.g. <c>"Layout.Email"</c>).
/// Resolved through the standard <see cref="Pipeline.ITemplateResolver"/> chain.
/// </param>
/// <param name="Priority">
/// Resolution priority when multiple prefix patterns match. Higher wins.
/// Exact matches always beat prefix matches regardless of priority.
/// Default: 0.
/// </param>
public sealed record LayoutRegistration(
    string Pattern,
    string LayoutTemplateName,
    int Priority = 0);
