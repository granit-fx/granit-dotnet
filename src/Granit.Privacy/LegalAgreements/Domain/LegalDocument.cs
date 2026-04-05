using Granit.Domain;
using Granit.Privacy.LegalAgreements.Events;
using Granit.Workflow.Domain;

namespace Granit.Privacy.LegalAgreements.Domain;

/// <summary>
/// A managed legal document with full version lifecycle (Draft → Published → Archived).
/// </summary>
/// <remarks>
/// <para>
/// Each version is a separate row sharing the same <see cref="VersionedWorkflowEntity.VersionId"/>.
/// The <see cref="VersionedWorkflowEntity.Version"/> integer is auto-incremented by
/// <c>VersioningInterceptor</c> on insert and used as the canonical version string
/// for consent tracking.
/// </para>
/// <para>
/// When a new version is published, the previous published version is archived and
/// <see cref="LegalAgreementObsoleteEto"/> is dispatched to trigger re-consent flows.
/// </para>
/// <para>
/// Rendered content lives in <c>Granit.Templating</c> (soft-dependency): set
/// <see cref="TemplateName"/> to reference a template by name (e.g., <c>"Legal.PrivacyPolicy"</c>).
/// The template is resolved by culture and rendered via <c>ITemplateEngine</c>.
/// </para>
/// </remarks>
public sealed class LegalDocument : VersionedWorkflowEntity, IMultiTenant, IWorkflowStateful
{
    private LegalDocument() { }

    /// <summary>Creates a new legal document draft.</summary>
    public static LegalDocument Create(
        Guid id,
        string documentId,
        string displayName,
        string? description = null,
        string? templateName = null,
        Guid? pdfBlobId = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(documentId);
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        return new LegalDocument
        {
            Id = id,
            DocumentId = documentId,
            DisplayName = displayName,
            Description = description,
            TemplateName = templateName,
            DocumentBlobId = pdfBlobId,
        };
    }

    /// <summary>Stable business identifier (e.g., <c>"privacy-policy"</c>, <c>"cookie-policy"</c>).</summary>
    public string DocumentId { get; private set; } = string.Empty;

    /// <summary>Human-readable document title.</summary>
    public string DisplayName { get; private set; } = string.Empty;

    /// <summary>Admin-only changelog note describing what changed in this version.</summary>
    public string? Description { get; private set; }

    /// <summary>
    /// Optional reference to a <c>Granit.Templating</c> template for rendered HTML content.
    /// Null when Templating is not loaded or when the document is PDF-only.
    /// </summary>
    public string? TemplateName { get; private set; }

    /// <summary>Optional reference to a <c>Granit.BlobStorage</c> blob for the downloadable document (PDF, DOCX, etc.).</summary>
    public Guid? DocumentBlobId { get; private set; }

    /// <inheritdoc/>
    public Guid? TenantId { get; private set; }

    /// <summary>Explicit interface for interceptor injection.</summary>
    Guid? IMultiTenant.TenantId { get => TenantId; set => TenantId = value; }

    /// <inheritdoc/>
    static string IWorkflowStateful.WorkflowEntityType => "LegalDocument";

    /// <summary>Updates draft metadata. Only allowed in Draft status.</summary>
    public void UpdateDraft(string displayName, string? description, string? templateName, Guid? pdfBlobId)
    {
        EnsureDraft();
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        DisplayName = displayName;
        Description = description;
        TemplateName = templateName;
        DocumentBlobId = pdfBlobId;
    }

    /// <summary>Attaches a document blob (PDF, DOCX, etc.) to this version. Only allowed in Draft status.</summary>
    public void AttachDocument(Guid blobId)
    {
        EnsureDraft();
        DocumentBlobId = blobId;
    }

    /// <summary>Publishes this document version, making it the active version.</summary>
    public void Publish()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Legal document '{Id}' is in '{LifecycleStatus}' status. Only drafts can be published.");
        }

        SetLifecycleStatus(WorkflowLifecycleStatus.Published);
    }

    /// <summary>
    /// Archives this document version, marking it as superseded by a newer version.
    /// Dispatches <see cref="LegalAgreementObsoleteEto"/> for re-consent flows.
    /// </summary>
    /// <param name="newVersion">The version number of the document that supersedes this one.</param>
    public void Archive(string newVersion)
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Published)
        {
            throw new InvalidOperationException(
                $"Legal document '{Id}' is in '{LifecycleStatus}' status. Only published documents can be archived.");
        }

        SetLifecycleStatus(WorkflowLifecycleStatus.Archived);

        AddDistributedEvent(new LegalAgreementObsoleteEto(
            DocumentId, Version.ToString(), newVersion));
    }

    private void EnsureDraft()
    {
        if (LifecycleStatus != WorkflowLifecycleStatus.Draft)
        {
            throw new InvalidOperationException(
                $"Legal document '{Id}' is in '{LifecycleStatus}' status. Only drafts can be modified.");
        }
    }
}
