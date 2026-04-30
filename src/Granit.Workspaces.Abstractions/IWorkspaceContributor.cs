namespace Granit.Workspaces;

/// <summary>
/// Cross-module hook for grafting onto someone else's workspace. Each
/// implementation is registered via
/// <c>services.AddWorkspaceContribution&lt;T&gt;()</c>; at boot the runtime
/// invokes every contributor in registration order to populate the final
/// workspace tree (per ADR-040 §IoC).
/// </summary>
/// <remarks>
/// Used so that, for example, <c>Granit.BlobStorage.Endpoints</c> can graft
/// its admin entries onto the <c>Granit.Framework.Data</c> shell without
/// either side taking a runtime dependency on the other — both pull only
/// <c>Granit.Workspaces.Abstractions</c>.
/// </remarks>
public interface IWorkspaceContributor
{
    /// <summary>
    /// Apply this module's contributions to the in-progress workspace tree.
    /// Sections / items added via <paramref name="context"/> are merged into
    /// the target workspace's compiled descriptor at boot time. Targeting a
    /// workspace that is not registered fails fast with a debug log — there
    /// is no silent skip in strict mode (see <c>WorkspaceComposerOptions</c>).
    /// </summary>
    void Contribute(IWorkspaceContributionContext context);
}

/// <summary>
/// Surface a contributor uses to graft onto a target workspace.
/// </summary>
public interface IWorkspaceContributionContext
{
    /// <summary>
    /// Returns a builder targeting the workspace identified by
    /// <paramref name="workspaceName"/>. Subsequent <c>.Section(...)</c>
    /// calls on the returned builder are merged into the target workspace.
    /// </summary>
    IWorkspaceContributionBuilder ForWorkspace(string workspaceName);
}

/// <summary>
/// Builder returned by <see cref="IWorkspaceContributionContext.ForWorkspace"/> — adds
/// sections to the target workspace.
/// </summary>
public interface IWorkspaceContributionBuilder
{
    /// <summary>
    /// Adds (or augments) a section on the target workspace. When a section
    /// with the same <paramref name="key"/> already exists on the target,
    /// the contributed items are appended after the existing ones, preserving
    /// declaration order across contributors.
    /// </summary>
    IWorkspaceContributionBuilder Section(string key, Action<WorkspaceSectionBuilder> configure);
}
