namespace Granit.Entities.Options;

/// <summary>
/// How the boot-time integrity check (story #1541) reacts to an
/// <see cref="EntityDefinition{TEntity}"/> citing a <c>QueryDefinition</c> /
/// <c>ExportDefinition</c> / <c>MetricDefinition</c> / <c>DashboardDefinition</c> /
/// <c>IWorkflowDefinition</c> that is not registered in DI.
/// </summary>
public enum IntegrityCheckMode
{
    /// <summary>Throw <see cref="InvalidOperationException"/> at host start. Recommended default.</summary>
    Throw,

    /// <summary>Log a warning per violation but let the host start. Use in transitional environments.</summary>
    Warn,

    /// <summary>Skip the check entirely. Discouraged outside of low-risk dev scenarios.</summary>
    Off,
}
