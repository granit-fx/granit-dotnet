using Granit.Modularity;

namespace Granit.Workspaces;

/// <summary>
/// Granit module marker for the workspaces abstractions package. Same pattern
/// as <c>GranitEntitiesAbstractionsModule</c> — pull this from any base module
/// that declares a <see cref="WorkspaceDefinition"/> or implements
/// <see cref="IFeatureProvider"/>; pull <c>GranitWorkspacesModule</c> only
/// from hosts that resolve and serve the tree.
/// </summary>
public sealed class GranitWorkspacesAbstractionsModule : GranitModule;
