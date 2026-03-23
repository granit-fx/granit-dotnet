using Granit.Querying;
using Granit.Workflow.Dtos;

namespace Granit.Workflow;

/// <summary>
/// Query service for retrieving workflow transition history from the database.
/// Implemented by the host application using its DbContext.
/// </summary>
/// <remarks>
/// This interface decouples the endpoints from any specific DbContext implementation.
/// The host application provides an implementation that queries
/// <c>WorkflowTransitionRecord</c> entities.
/// </remarks>
public interface IWorkflowHistoryQuery
{
    /// <summary>
    /// Returns the paginated transition history for a specific entity, ordered chronologically.
    /// </summary>
    /// <param name="entityType">Logical entity type name.</param>
    /// <param name="entityId">Entity identifier.</param>
    /// <param name="page">One-based page number.</param>
    /// <param name="pageSize">Maximum number of entries per page.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task<PagedResult<WorkflowTransitionHistoryResponse>> GetHistoryAsync(
        string entityType,
        string entityId,
        int page = 1,
        int pageSize = QueryingDefaults.DefaultPageSize,
        CancellationToken cancellationToken = default);
}
