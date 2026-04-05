using Granit.Domain;
using Granit.Workflow.Domain;

namespace Granit.Templating.EntityFrameworkCore.Entities;

/// <summary>
/// EF Core entity mapping a single revision of a template.
/// </summary>
/// <remarks>
/// <para>
/// Inherits from <see cref="VersionedWorkflowEntity"/> to gain automatic versioning
/// (<see cref="IVersioned.VersionId"/> + <see cref="IVersioned.Version"/>), workflow
/// lifecycle management (<see cref="WorkflowLifecycleStatus"/>), and ISO 27001 audit
/// trail via <see cref="WorkflowTransitionRecord"/>.
/// </para>
/// <para>
/// Revisions are append-only once promoted beyond <c>Draft</c>.
/// Published and archived revisions are preserved indefinitely (ISO 27001 audit trail).
/// Implements <see cref="IMultiTenant"/> for per-tenant template isolation.
/// </para>
/// </remarks>
internal sealed class TemplateRevisionEntity : VersionedWorkflowEntity, IMultiTenant, IWorkflowStateful
{
    /// <summary>
    /// EF Core materialization constructor.
    /// </summary>
    private TemplateRevisionEntity()
    {
    }

    /// <summary>Logical entity type name for the workflow audit trail.</summary>
    static string IWorkflowStateful.WorkflowEntityType => "TemplateRevision";

    /// <summary>
    /// Computed alias for backward compatibility with the rendering pipeline.
    /// Maps to <see cref="Entity.Id"/> (the entity PK).
    /// </summary>
    public Guid RevisionId => Id;

    /// <summary>Template name (matches <c>TemplateKey.Name</c>).</summary>
    public string TemplateName { get; private set; } = null!;

    /// <summary>BCP-47 culture tag, or <c>null</c> for culture-neutral templates.</summary>
    public string? Culture { get; private set; }

    /// <summary>Raw template source content (HTML).</summary>
    public string Content { get; private set; } = null!;

    /// <summary>MIME type of the template content (e.g. <c>"text/html"</c>).</summary>
    public string MimeType { get; private set; } = null!;

    /// <summary>UTC timestamp when this revision was published. <c>null</c> for drafts.</summary>
    public DateTimeOffset? PublishedAt { get; private set; }

    /// <summary>Identity of the user who published this revision. <c>null</c> for drafts.</summary>
    public string? PublishedBy { get; private set; }

    /// <summary>Optional category for organizing templates by domain.</summary>
    public Guid? CategoryId { get; private set; }

    /// <summary>
    /// Layout template name assigned by an administrator.
    /// Overrides the code-level <c>ILayoutRegistry</c> default.
    /// <c>null</c> means "use the registry default".
    /// </summary>
    public string? LayoutName { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor write access.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <summary>
    /// Creates a new template revision in <see cref="WorkflowLifecycleStatus.Draft"/> status.
    /// </summary>
    public static TemplateRevisionEntity Create(
        Guid id,
        string templateName,
        string? culture,
        string content,
        string mimeType,
        string? layoutName = null,
        Guid? categoryId = null)
    {
        return new TemplateRevisionEntity
        {
            Id = id,
            TemplateName = templateName,
            Culture = culture,
            Content = content,
            MimeType = mimeType,
            LayoutName = layoutName,
            CategoryId = categoryId,
        };
    }

    /// <summary>
    /// Sets the version group so the <c>VersioningInterceptor</c> increments
    /// the version within an existing group instead of creating a new one.
    /// </summary>
    internal TemplateRevisionEntity WithVersionId(Guid versionId)
    {
        ((IVersioned)this).VersionId = versionId;
        return this;
    }

    /// <summary>
    /// Updates the draft content in-place (only drafts are editable).
    /// </summary>
    public void UpdateDraft(string content, string mimeType, string? layoutName)
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException("Only drafts can be updated.");
        }

        Content = content;
        MimeType = mimeType;
        LayoutName = layoutName;
    }

    /// <summary>
    /// Promotes this draft revision to <see cref="WorkflowLifecycleStatus.Published"/>.
    /// </summary>
    public void Publish(string publishedBy, DateTimeOffset publishedAt)
    {
        SetLifecycleStatus(WorkflowLifecycleStatus.Published);
        PublishedAt = publishedAt;
        PublishedBy = publishedBy;
    }

    /// <summary>
    /// Archives this revision (superseded by a newer publication).
    /// </summary>
    public void Archive() =>
        SetLifecycleStatus(WorkflowLifecycleStatus.Archived);
}
