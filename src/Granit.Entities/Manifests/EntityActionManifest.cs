using Granit.Entities.Actions;

namespace Granit.Entities.Manifests;

/// <summary>
/// Wire shape for one action exposed on an entity manifest. Mirrors
/// <see cref="EntityActionDescriptor"/> with the contributor-assembly
/// attribution carried over so the SPA can render an "Added by {Module}"
/// affordance.
/// </summary>
/// <param name="Name">Stable action name, unique per entity.</param>
/// <param name="Kind">Renderer dispatch — drives which payload fields the frontend reads.</param>
/// <param name="DisplayKey">i18n key for the user-facing label.</param>
/// <param name="Icon">Icon name from the catalog.</param>
/// <param name="Order">Display order among the entity's actions.</param>
/// <param name="UrlTemplate">URL template with <c>{id}</c> placeholder. <see langword="null"/> for WorkflowTransition, OpenDrawer (without explicit URL — renderer falls back to <c>details["default"]</c>) and OpenModal (without explicit URL — renderer falls back to <c>forms["default"]</c>).</param>
/// <param name="HttpMethod">HTTP verb for ApiCall (POST / PUT / DELETE). <see langword="null"/> otherwise.</param>
/// <param name="ConfirmationKey">Optional i18n key for the confirmation modal.</param>
/// <param name="WorkflowTransitionName">Name of the target workflow state for WorkflowTransition.</param>
/// <param name="ContributorAssemblyName">Contributing assembly. <see langword="null"/> for intra-module declarations.</param>
public sealed record EntityActionManifest(
    string Name,
    EntityActionKind Kind,
    string? DisplayKey,
    string? Icon,
    int Order,
    string? UrlTemplate,
    string? HttpMethod,
    string? ConfirmationKey,
    string? WorkflowTransitionName,
    string? ContributorAssemblyName);
