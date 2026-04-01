using Granit.Templating.Store;

namespace Granit.Templating.Endpoints.Dtos;

/// <summary>
/// Response item for the paginated template list endpoint.
/// </summary>
/// <param name="Name">Logical template name (e.g. <c>"Billing.Invoice"</c>).</param>
/// <param name="Culture">BCP 47 culture tag, or <c>null</c> for culture-neutral templates.</param>
/// <param name="MimeType">MIME type of the template content.</param>
/// <param name="CurrentStatus">Current lifecycle status (Draft if a draft exists, else Published).</param>
/// <param name="LastModifiedAt">UTC timestamp of the most recent modification.</param>
/// <param name="LastModifiedBy">Identity of the user who last modified the template.</param>
/// <param name="HasPublishedVersion">Whether a published version currently exists for this key.</param>
/// <param name="LayoutName">
/// Layout template name assigned to this template.
/// <c>null</c> means the code-level <c>ILayoutRegistry</c> default applies.
/// </param>
public sealed record TemplateListItemResponse(
    string Name,
    string? Culture,
    string MimeType,
    TemplateLifecycleStatus CurrentStatus,
    DateTimeOffset LastModifiedAt,
    string LastModifiedBy,
    bool HasPublishedVersion,
    string? LayoutName);
