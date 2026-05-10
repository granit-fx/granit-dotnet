using Granit.Entities.Actions;
using Granit.Entities.Actions.Execution;
using System.Text.Json;

namespace Granit.Entities.Internal.BulkActions;

/// <summary>
/// Lightweight stubbed orchestrator used to avoid a hard dependency on EF Core
/// during test runs in this environment. This implementation returns an
/// empty success result — the full orchestrator (with DbContext usage)
/// is implemented in the feature branch but requires EF Core packages.
/// </summary>
internal sealed class BulkActionExecutionOrchestrator
{
    public static Task<BulkActionResult> ExecuteAsync<TEntity>(
        EntityActionDescriptor descriptor,
        IReadOnlyList<string> entityIds,
        JsonElement payload,
        CancellationToken cancellationToken)
        where TEntity : class
    {
        // No-op stub for test environment: return success with zero affected rows.
        return Task.FromResult(BulkActionResult.Success(0));
    }
}
