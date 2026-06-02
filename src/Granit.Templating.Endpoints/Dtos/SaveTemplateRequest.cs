namespace Granit.Templating.Endpoints.Dtos;

/// <summary>
/// Request body for creating or updating a template draft.
/// </summary>
/// <param name="Name">
/// Logical template name using <c>"Domain.Name"</c> convention (e.g. <c>"Billing.Invoice"</c>).
/// Required for POST (create). Ignored for PUT (name comes from the route).
/// </param>
/// <param name="Culture">Optional BCP 47 culture tag (e.g. <c>"fr-BE"</c>). <c>null</c> for culture-neutral.</param>
/// <param name="Content">Template source content (Scriban HTML).</param>
/// <param name="MimeType">MIME type of the content. Default: <c>"text/html"</c>.</param>
/// <param name="LayoutName">
/// Layout template name assigned to this template.
/// <c>null</c> means the code-level <c>ILayoutRegistry</c> default applies.
/// </param>
/// <param name="ConcurrencyStamp">
/// Stamp from the last read of the draft revision.
/// Provide when updating an existing draft to detect concurrent modifications (HTTP 409).
/// Omit or pass <c>null</c> when creating the first draft for this template key.
/// </param>
public sealed record SaveTemplateRequest(
    string? Name,
    string? Culture,
    string Content,
    string MimeType = "text/html",
    string? LayoutName = null,
    string? ConcurrencyStamp = null);
