using Granit.Invoicing.Domain;
using Granit.Workflow;

namespace Granit.Invoicing.Definitions;

/// <summary>Invoice lifecycle workflow definition (5 states).</summary>
public static class InvoiceWorkflows
{
    /// <summary>The standard invoice workflow (initial state: Draft).</summary>
    public static WorkflowDefinition<InvoiceStatus> Default { get; } =
        WorkflowDefinition<InvoiceStatus>.Create(builder => builder
            .InitialState(InvoiceStatus.Draft)

            .Transition(InvoiceStatus.Draft, InvoiceStatus.Open, t => t
                .Named("Finalize"))

            .Transition(InvoiceStatus.Open, InvoiceStatus.Paid, t => t
                .Named("Mark Paid"))

            .Transition(InvoiceStatus.Open, InvoiceStatus.Void, t => t
                .Named("Void"))

            .Transition(InvoiceStatus.Open, InvoiceStatus.Uncollectible, t => t
                .Named("Mark Uncollectible"))

            .Transition(InvoiceStatus.Uncollectible, InvoiceStatus.Paid, t => t
                .Named("Recover Payment"))

            .Transition(InvoiceStatus.Uncollectible, InvoiceStatus.Void, t => t
                .Named("Void Uncollectible")));
}
