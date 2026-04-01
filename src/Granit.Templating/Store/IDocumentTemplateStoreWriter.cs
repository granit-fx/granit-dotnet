using Granit.Templating.Keys;

namespace Granit.Templating.Store;

/// <summary>
/// Write-side contract for managing the draft/published/archived lifecycle of templates.
/// </summary>
/// <remarks>
/// Lifecycle:
/// <list type="number">
///   <item><see cref="SaveDraftAsync"/> — create or update the editable draft for a key.</item>
///   <item><see cref="PublishAsync"/> — promote the current draft to <c>Published</c>,
///     archiving any previous published revision.</item>
///   <item><see cref="UnpublishAsync"/> — archive the published revision without promoting a new one.</item>
///   <item><see cref="DeleteDraftAsync"/> — physically delete a draft (only allowed for drafts).</item>
/// </list>
/// Published and archived revisions are never physically deleted (ISO 27001 requirement).
/// </remarks>
public interface IDocumentTemplateStoreWriter
{
    /// <summary>
    /// Creates or replaces the draft for the given key.
    /// </summary>
    /// <remarks>
    /// Only one draft per key is maintained. Calling this method multiple times
    /// replaces the previous draft content.
    /// </remarks>
    /// <param name="key">Template key.</param>
    /// <param name="content">Template source (HTML).</param>
    /// <param name="mimeType">MIME type (e.g. <c>"text/html"</c>).</param>
    /// <param name="updatedBy">Identity of the user saving the draft.</param>
    /// <param name="layoutName">
    /// Optional layout template name. Overrides the code-level <c>ILayoutRegistry</c>
    /// default. Pass <c>null</c> to use the registry default.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task SaveDraftAsync(
        TemplateKey key,
        string content,
        string mimeType,
        string updatedBy,
        string? layoutName = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Promotes the current draft to <c>Published</c>, archiving any previous published revision.
    /// </summary>
    /// <param name="key">Template key.</param>
    /// <param name="publishedBy">Identity of the user publishing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">No draft exists for this key.</exception>
    Task PublishAsync(
        TemplateKey key,
        string publishedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Archives the currently published revision without promoting a new one.
    /// After this call, <see cref="IDocumentTemplateStoreReader.TryGetPublishedAsync"/> returns <c>null</c> for this key.
    /// </summary>
    /// <param name="key">Template key.</param>
    /// <param name="unpublishedBy">Identity of the user unpublishing.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task UnpublishAsync(
        TemplateKey key,
        string unpublishedBy,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Physically deletes the draft for the given key.
    /// </summary>
    /// <remarks>
    /// Only drafts may be deleted. Published and archived revisions are preserved
    /// permanently for ISO 27001 compliance.
    /// </remarks>
    /// <param name="key">Template key.</param>
    /// <param name="deletedBy">Identity of the user performing the deletion.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <exception cref="InvalidOperationException">No draft exists, or the revision is not a draft.</exception>
    Task DeleteDraftAsync(
        TemplateKey key,
        string deletedBy,
        CancellationToken cancellationToken = default);
}
