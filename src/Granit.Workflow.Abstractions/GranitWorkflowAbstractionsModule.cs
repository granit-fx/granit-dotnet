using Granit.Modularity;

namespace Granit.Workflow;

/// <summary>
/// Granit module marker for the workflow abstractions package.
/// </summary>
/// <remarks>
/// This module has no service registrations — it exists so a base module can declare
/// <c>[DependsOn(typeof(GranitWorkflowAbstractionsModule))]</c> and reference workflow
/// definition types (<see cref="IWorkflowDefinition{TState}"/>, <see cref="WorkflowDefinition{TState}"/>,
/// <see cref="WorkflowDefinitionBuilder{TState}"/>, …) without taking a runtime dependency
/// on the Granit.Workflow engine, the in-memory permission checker, or the transition
/// recorder. Same pattern as <c>GranitQueryEngineAbstractionsModule</c>.
/// </remarks>
public sealed class GranitWorkflowAbstractionsModule : GranitModule;
