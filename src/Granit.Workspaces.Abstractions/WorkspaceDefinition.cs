namespace Granit.Workspaces;

/// <summary>
/// Base class for declaring a workspace — the fluent C# entry point that drives
/// the <c>Granit.Workspaces</c> navigation tree (per ADR-040). Mirrors
/// <c>EntityDefinition&lt;TEntity&gt;</c> / <c>QueryDefinition&lt;TEntity&gt;</c>.
/// </summary>
/// <remarks>
/// Each workspace definition is registered as a singleton via
/// <c>services.AddWorkspaceDefinition&lt;T&gt;()</c>. <see cref="Configure"/> is
/// called exactly once per process; the resulting descriptor is cached and exposed
/// through <see cref="IWorkspaceDescriptor"/>.
/// </remarks>
public abstract class WorkspaceDefinition : IWorkspaceDescriptor
{
    private WorkspaceDescriptor? _descriptor;
    private readonly Lock _buildLock = new();

    /// <summary>Wire identifier (e.g. <c>"Granit.Framework"</c>, <c>"Showcase.Crm"</c>). MUST be unique.</summary>
    public abstract string Name { get; }

    /// <inheritdoc />
    public WorkspaceDescriptor Descriptor
    {
        get
        {
            if (_descriptor is not null)
            {
                return _descriptor;
            }

            lock (_buildLock)
            {
                if (_descriptor is null)
                {
                    WorkspaceBuilder builder = new();
                    Configure(builder);
                    _descriptor = builder.Build(Name);
                }
            }

            return _descriptor;
        }
    }

    /// <summary>
    /// Configure the workspace's sections + items using the fluent builder.
    /// Called exactly once per process; the resulting descriptor is immutable
    /// and cached.
    /// </summary>
    /// <param name="builder">The fluent workspace builder.</param>
    protected abstract void Configure(WorkspaceBuilder builder);
}
