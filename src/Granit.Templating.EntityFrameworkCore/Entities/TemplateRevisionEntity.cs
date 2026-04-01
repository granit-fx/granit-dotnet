using Granit.Domain;
using Granit.Templating.Store;

namespace Granit.Templating.EntityFrameworkCore.Entities;

/// <summary>
/// EF Core entity mapping a single revision of a template.
/// </summary>
/// <remarks>
/// Revisions are append-only once promoted beyond <c>Draft</c>.
/// Published and archived revisions are preserved indefinitely (ISO 27001 audit trail).
/// Implements <see cref="IMultiTenant"/> for per-tenant template isolation.
/// </remarks>
internal sealed class TemplateRevisionEntity : IMultiTenant
{
    /// <summary>Unique identifier propagated into rendered documents for traceability.</summary>
    public Guid RevisionId { get; set; }

    /// <summary>Template name (matches <c>TemplateKey.Name</c>).</summary>
    public string TemplateName { get; set; } = null!;

    /// <summary>BCP-47 culture tag, or <c>null</c> for culture-neutral templates.</summary>
    public string? Culture { get; set; }

    /// <summary>Raw template source content (HTML).</summary>
    public string Content { get; set; } = null!;

    /// <summary>MIME type of the template content (e.g. <c>"text/html"</c>).</summary>
    public string MimeType { get; set; } = null!;

    /// <summary>Lifecycle status.</summary>
    public TemplateLifecycleStatus Status { get; set; }

    /// <summary>UTC timestamp when this revision was saved as draft.</summary>
    public DateTimeOffset CreatedAt { get; set; }

    /// <summary>Identity of the user who last saved or created this draft.</summary>
    public string CreatedBy { get; set; } = null!;

    /// <summary>UTC timestamp when this revision was published. <c>null</c> for drafts.</summary>
    public DateTimeOffset? PublishedAt { get; set; }

    /// <summary>Identity of the user who published this revision. <c>null</c> for drafts.</summary>
    public string? PublishedBy { get; set; }

    /// <summary>UTC timestamp when this revision was archived. <c>null</c> unless archived.</summary>
    public DateTimeOffset? ArchivedAt { get; set; }

    /// <summary>Identity of the user who archived this revision. <c>null</c> unless archived.</summary>
    public string? ArchivedBy { get; set; }

    /// <summary>Optional category for organizing templates by domain.</summary>
    public Guid? CategoryId { get; set; }

    /// <summary>
    /// Layout template name assigned by an administrator.
    /// Overrides the code-level <c>ILayoutRegistry</c> default.
    /// <c>null</c> means "use the registry default".
    /// </summary>
    public string? LayoutName { get; set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; set; }
}
