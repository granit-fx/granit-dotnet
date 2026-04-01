namespace Granit.Templating.Store;

/// <summary>
/// Summary projection of a template for list views.
/// Groups revisions by <c>(Name, Culture)</c> and exposes the most relevant metadata.
/// </summary>
public sealed class TemplateSummary
{
    /// <summary>Logical template name (e.g. <c>"Billing.Invoice"</c>).</summary>
    public required string Name { get; init; }

    /// <summary>BCP 47 culture tag, or <c>null</c> for culture-neutral templates.</summary>
    public required string? Culture { get; init; }

    /// <summary>MIME type of the template content.</summary>
    public required string MimeType { get; init; }

    /// <summary>
    /// Current lifecycle status. Returns <see cref="TemplateLifecycleStatus.Draft"/> if a draft exists,
    /// otherwise the status of the published revision.
    /// </summary>
    public required TemplateLifecycleStatus CurrentStatus { get; init; }

    /// <summary>UTC timestamp of the most recent modification (draft save or publication).</summary>
    public required DateTimeOffset LastModifiedAt { get; init; }

    /// <summary>Identity of the user who last modified the template.</summary>
    public required string LastModifiedBy { get; init; }

    /// <summary>Whether a published version currently exists for this key.</summary>
    public required bool HasPublishedVersion { get; init; }

    /// <summary>
    /// Layout template name assigned by an administrator.
    /// <c>null</c> means "use the <see cref="Layouts.ILayoutRegistry"/> default".
    /// </summary>
    public string? LayoutName { get; init; }
}
