using Granit.QueryEngine;
using Granit.Workflow.Domain;
using Microsoft.EntityFrameworkCore;

namespace Granit.Workflow.EntityFrameworkCore.Internal;

/// <summary>
/// EF Core implementation of <see cref="IQueryableSource{TEntity}"/> for
/// <see cref="WorkflowTransitionRecord"/>, backing <c>MapGranitQuery&lt;WorkflowTransitionRecord&gt;</c>
/// and the analytics runner over <c>WorkflowTransitionRecordQuery</c>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike the other Granit query sources, workflow transition records are not stored in a
/// framework-owned isolated DbContext: the host's own <typeparamref name="TDbContext"/>
/// implements <see cref="IWorkflowDbContext"/> and owns the table. The scoped host context
/// is therefore injected directly (the DI container owns its lifetime — no factory, no dispose
/// here), exactly like <see cref="DefaultWorkflowHistoryQuery{TDbContext}"/>.
/// </para>
/// <para>
/// No <c>IMultiTenant</c> query-filter bypass is applied: the named multi-tenant filter is not
/// guaranteed to exist on an arbitrary host context (it is only present when the host context
/// derives from <c>GranitDbContext</c> / applies <c>ApplyGranitConventions</c>), and calling
/// <c>IgnoreQueryFilters</c> for an absent named filter throws. This mirrors the no-bypass
/// behaviour of <see cref="DefaultWorkflowHistoryQuery{TDbContext}"/>.
/// </para>
/// </remarks>
internal sealed class EfWorkflowTransitionRecordQueryableSource<TDbContext>(TDbContext dbContext)
    : IQueryableSource<WorkflowTransitionRecord>
    where TDbContext : DbContext, IWorkflowDbContext
{
    public IQueryable<WorkflowTransitionRecord> GetQueryable() =>
        dbContext.WorkflowTransitionRecords.AsNoTracking();
}
