using System.Diagnostics;

namespace Granit.Workflow.Diagnostics;

/// <summary>
/// Central <see cref="ActivitySource"/> for Granit.Workflow distributed tracing.
/// </summary>
/// <remarks>
/// <c>Granit.Observability</c> adds this source automatically via
/// <c>GranitActivitySourceRegistry</c> when both packages are used.
/// </remarks>
internal static class WorkflowActivitySource
{
    /// <summary>The name of the Granit.Workflow <see cref="ActivitySource"/>.</summary>
    internal const string Name = "Granit.Workflow";

    /// <summary>The singleton <see cref="ActivitySource"/> instance.</summary>
    internal static readonly ActivitySource Source = new(Name);

    // ---- Operation names ----

    internal const string Transition = "workflow.transition";
}
