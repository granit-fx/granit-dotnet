using Granit.Workflow.Domain;

namespace Granit.Privacy.LegalAgreements.Exceptions;

/// <summary>
/// Thrown when publication is requested for a legal document that is not in
/// <see cref="WorkflowLifecycleStatus.Draft"/> — only drafts can be published.
/// </summary>
/// <remarks>
/// Distinct from a 404 — the document exists, it just isn't in a publishable state. The
/// endpoint surface maps this to <c>400 Bad Request</c>.
/// </remarks>
public sealed class LegalDocumentNotPublishableException(Guid documentId, WorkflowLifecycleStatus status)
    : Exception($"Legal document '{documentId}' is in '{status}' status. Only drafts can be published.")
{
    /// <summary>Identifier of the legal document that could not be published.</summary>
    public Guid DocumentId { get; } = documentId;

    /// <summary>The document's current lifecycle status that blocked publication.</summary>
    public WorkflowLifecycleStatus Status { get; } = status;
}
