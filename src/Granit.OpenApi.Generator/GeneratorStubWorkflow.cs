using Granit.Workflow;

namespace Granit.OpenApi.Generator;

/// <summary>
/// Placeholder workflow state enum registered only by the contract generator so the generic
/// transition endpoints (<c>GET /transitions</c>, <c>POST /transitions</c>) can be mounted and appear
/// in the emitted OpenAPI document.
/// </summary>
/// <remarks>
/// <c>MapGranitWorkflowTransition&lt;TState&gt;</c> is generic over the host application's state enum,
/// so those two routes materialize only once some enum is supplied — without this placeholder the
/// workflow contract ships with the history route alone and the two <c>/transitions</c> routes the
/// frontend legitimately calls read as orphan endpoints. The name is deliberately generic (not
/// <c>GeneratorStub…</c>): the endpoints derive their <c>operationId</c> from
/// <c>typeof(TState).Name</c>, so it lands in the published contract.
/// </remarks>
internal enum WorkflowState
{
    /// <summary>Initial state of the placeholder workflow.</summary>
    Draft,

    /// <summary>Terminal state of the placeholder workflow.</summary>
    Published,
}

/// <summary>
/// Minimal two-state workflow definition backing <see cref="WorkflowState"/>, registered only by the
/// contract generator to satisfy the <c>IWorkflowManager&lt;TState&gt;</c> requirement of the generic
/// transition endpoints (see <see cref="WorkflowState"/> for why the placeholder exists).
/// </summary>
/// <remarks>
/// Mirrors <see cref="GeneratorStubGeocodingProvider"/>: it never runs at request time — the generator
/// only probes minimal-API bindings — so the transition set is the smallest one that still describes a
/// well-formed finite state machine.
/// </remarks>
internal sealed class GeneratorStubWorkflowDefinition : IWorkflowDefinition<WorkflowState>
{
    private static readonly WorkflowTransition<WorkflowState>[] AllTransitions =
        [new() { From = WorkflowState.Draft, To = WorkflowState.Published }];

    public WorkflowState InitialState => WorkflowState.Draft;

    public IReadOnlyList<WorkflowTransition<WorkflowState>> Transitions => AllTransitions;

    public IReadOnlyList<WorkflowTransition<WorkflowState>> GetAllowedTransitions(WorkflowState from) =>
        [.. AllTransitions.Where(transition => transition.From.Equals(from))];
}
