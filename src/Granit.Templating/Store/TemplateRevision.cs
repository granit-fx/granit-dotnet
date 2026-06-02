using Granit.Workflow.Domain;

namespace Granit.Templating.Store;

/// <summary>
/// Represents a historical revision of a template returned by <see cref="IDocumentTemplateStoreReader"/>.
/// </summary>
public sealed class TemplateRevision
{
    /// <summary>Unique identifier of this revision (propagated into rendered documents).</summary>
    public required Guid RevisionId { get; init; }

    /// <summary>Raw template content (HTML source).</summary>
    public required string Content { get; init; }

    /// <summary>MIME type of the template content.</summary>
    public required string MimeType { get; init; }

    /// <summary>Lifecycle status at the time of retrieval.</summary>
    public required WorkflowLifecycleStatus Status { get; init; }

    /// <summary>Monotonically increasing version number within the same template key.</summary>
    public required int Version { get; init; }

    /// <summary>UTC timestamp when this revision was created.</summary>
    public required DateTimeOffset CreatedAt { get; init; }

    /// <summary>Identity of the user who saved this draft.</summary>
    public required string CreatedBy { get; init; }

    /// <summary>UTC timestamp when this revision was published. Null for drafts.</summary>
    public DateTimeOffset? PublishedAt { get; init; }

    /// <summary>Identity of the user who published this revision. Null if not yet published.</summary>
    public string? PublishedBy { get; init; }

    /// <summary>Layout template name assigned by an administrator.</summary>
    public string? LayoutName { get; init; }

    /// <summary>Opaque optimistic-concurrency token. Pass back in update requests to detect concurrent modifications (HTTP 409).</summary>
    public string ConcurrencyStamp { get; init; } = string.Empty;
}
