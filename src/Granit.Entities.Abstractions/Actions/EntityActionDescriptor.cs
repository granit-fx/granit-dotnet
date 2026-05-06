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
/// <param name="UrlTemplate">URL template with <c>{id}</c> placeholder. Required for ApiCall, Download and Navigate; <see langword="null"/> for WorkflowTransition. Optional for <see cref="EntityActionKind.OpenDrawer"/> and <see cref="EntityActionKind.OpenModal"/> — when <see langword="null"/>, the renderer falls back to the entity's default detail / form layout.</param>
/// <param name="HttpMethod">HTTP verb for ApiCall (POST / PUT / DELETE). <see langword="null"/> otherwise.</param>
/// <param name="ConfirmationKey">Optional i18n key for the confirmation modal shown before invoking the action.</param>
/// <param name="WorkflowTransitionName">Name of the target workflow state for <see cref="EntityActionKind.WorkflowTransition"/>.</param>
/// <param name="ContributorAssemblyName">Name of the contributing assembly. <see langword="null"/> for intra-module declarations.</param>
/// <param name="ShowOnKanbanCard">When <see langword="true"/>, the action also appears as a compact icon-button on the source entity's kanban tile (Phase 2.B.2). Off by default — only the actions the contributor explicitly opts into via <c>OnKanbanCard()</c> are pinned, since kanban tiles have far less surface than the detail header.</param>
/// <param name="ShowOnGalleryCard">When <see langword="true"/>, the action also appears as a compact icon-button on the source entity's gallery card. Off by default — same surface-budget rationale as <see cref="ShowOnKanbanCard"/>.</param>
/// <param name="ShowOnCalendarTile">When <see langword="true"/>, the action also appears as a compact icon-button on the source entity's calendar tile. Off by default — calendar tiles are smaller than kanban cards so curate carefully.</param>
/// <param name="ShowOnListHeader">When <see langword="true"/>, the action is pinned on the list-page header (above the list / kanban / gallery / calendar tabs), not on individual rows. Use for entity-scope actions like <c>Import</c>, <c>Export</c>, <c>BulkArchive</c> — the URL template must NOT carry a <c>{id}</c> placeholder since no row is selected. Mirrors Odoo's top-of-list action bar.</param>
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
    string? ContributorAssemblyName,
    bool ShowOnKanbanCard = false,
    bool ShowOnGalleryCard = false,
    bool ShowOnCalendarTile = false,
    bool ShowOnListHeader = false);
