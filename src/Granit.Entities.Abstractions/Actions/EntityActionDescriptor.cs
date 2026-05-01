namespace Granit.Entities.Actions;

/// <summary>
/// Immutable descriptor for one action declared on an entity — covers both
/// intra-module declarations (<c>Action(...)</c> on
/// <see cref="EntityDefinitionBuilder{TEntity}"/>) and cross-module grafts
/// (<see cref="IEntityActionContributor"/>).
/// </summary>
/// <param name="Name">Stable action name, unique per source entity (e.g. <c>"finalize"</c>).</param>
/// <param name="Kind">Renderer dispatch — drives which payload fields the frontend reads.</param>
/// <param name="DisplayKey">i18n key for the user-facing label.</param>
/// <param name="Icon">Icon name from the icon catalog.</param>
/// <param name="Order">Display order among the entity's actions.</param>
/// <param name="RequiresPermission">Optional permission gate — drops the action from the manifest payload when the user does not hold it (defense in depth, never just hidden).</param>
/// <param name="UrlTemplate">URL template with <c>{id}</c> placeholder. Required for ApiCall, Download and Navigate; <see langword="null"/> for WorkflowTransition.</param>
/// <param name="HttpMethod">HTTP verb for ApiCall (POST / PUT / DELETE). <see langword="null"/> otherwise.</param>
/// <param name="ConfirmationKey">Optional i18n key for the confirmation modal shown before invoking the action.</param>
/// <param name="WorkflowTransitionName">Name of the target workflow state for <see cref="EntityActionKind.WorkflowTransition"/>.</param>
/// <param name="ContributorAssemblyName">Name of the contributing assembly. <see langword="null"/> for intra-module declarations.</param>
public sealed record EntityActionDescriptor(
    string Name,
    EntityActionKind Kind,
    string? DisplayKey,
    string? Icon,
    int Order,
    string? RequiresPermission,
    string? UrlTemplate,
    string? HttpMethod,
    string? ConfirmationKey,
    string? WorkflowTransitionName,
    string? ContributorAssemblyName);
