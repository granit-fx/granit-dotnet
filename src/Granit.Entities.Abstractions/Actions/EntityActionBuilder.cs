namespace Granit.Entities.Actions;

/// <summary>
/// Fluent builder for one entity action. Each kind-shortcut (<see cref="ApiCall"/>,
/// <see cref="Download"/>, <see cref="Navigate"/>, <see cref="WorkflowTransition"/>)
/// sets the discriminator and the kind-specific fields atomically — there is no way
/// to build an inconsistent descriptor (e.g. ApiCall without a URL).
/// </summary>
/// <typeparam name="TEntity">The entity the action is attached to. Reserved for future
/// type-safe shortcuts (e.g. expression-based URL building); currently unused but
/// kept for symmetry with <c>RelationBuilder&lt;TSource, TRelated&gt;</c>.</typeparam>
public sealed class EntityActionBuilder<TEntity>
    where TEntity : class
{
    private readonly string _name;
    private readonly string? _contributorAssemblyName;
    private readonly string? _routeBase;

    private EntityActionKind _kind = EntityActionKind.ApiCall;
    private string? _displayKey;
    private string? _icon;
    private int _order;
    private string? _requiresPermission;
    private string? _urlTemplate;
    private string? _httpMethod;
    private string? _confirmationKey;
    private string? _workflowTransitionName;
    private bool _showOnKanbanCard;
    private bool _showOnGalleryCard;
    private bool _showOnCalendarTile;
    private bool _showOnListHeader;

    // RouteBase composition state — populated by verb shortcuts (Post / Put / Delete /
    // Patch / Get / Download). At Build() time, if no explicit URL was supplied via
    // ApiCall(method, fullUrl) or AbsolutePath(...), the URL is composed as
    // {routeBase}/{id?}/{pathSegment ?? actionName}. {id} segment is omitted when
    // the action is pinned via OnListHeader().
    private bool _useRouteBase;
    private string? _pathSegment;
    private string? _absolutePath;

    internal EntityActionBuilder(string name, string? contributorAssemblyName = null, string? routeBase = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        _name = name;
        _contributorAssemblyName = contributorAssemblyName;
        _routeBase = routeBase;
    }

    /// <summary>
    /// Configures the action as an HTTP write call (POST / PUT / DELETE) against
    /// the explicit <paramref name="urlTemplate"/>. The template can reference
    /// <c>{id}</c> for the entity primary key; the renderer resolves it at click
    /// time. Escape hatch — prefer <see cref="Post"/> / <see cref="Put"/> /
    /// <see cref="Delete"/> when the entity has a <c>RouteBase</c>.
    /// </summary>
    public EntityActionBuilder<TEntity> ApiCall(string method, string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);

        _kind = EntityActionKind.ApiCall;
        _httpMethod = method.ToUpperInvariant();
        _urlTemplate = urlTemplate;
        _useRouteBase = false;
        _pathSegment = null;
        return this;
    }

    /// <summary>
    /// Configures the action as a binary download (HTTP GET). When the entity declares
    /// a <c>RouteBase</c>, the URL is composed as
    /// <c>{RouteBase}/{id}/{path ?? actionName}</c> (no <c>{id}</c> for header
    /// actions). When <c>RouteBase</c> is absent, <paramref name="path"/> is treated
    /// as the full URL (legacy behaviour — kept for backwards compatibility).
    /// </summary>
    public EntityActionBuilder<TEntity> Download(string? path = null)
    {
        _kind = EntityActionKind.Download;
        _httpMethod = null;
        _useRouteBase = true;
        _pathSegment = path;
        _urlTemplate = null;
        return this;
    }

    /// <summary>
    /// Configures the action as a client-side navigation (route or external URL).
    /// SPA URLs never compose from <c>RouteBase</c> — pass the full SPA path here.
    /// </summary>
    public EntityActionBuilder<TEntity> Navigate(string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);

        _kind = EntityActionKind.Navigate;
        _httpMethod = null;
        _urlTemplate = urlTemplate;
        _useRouteBase = false;
        _pathSegment = null;
        return this;
    }

    /// <summary>
    /// Verb shortcut: composes the URL as <c>POST {RouteBase}/{id}/{path ?? actionName}</c>
    /// (no <c>{id}</c> when the action is pinned on the list header).
    /// Requires the entity to declare a <c>RouteBase</c>.
    /// </summary>
    public EntityActionBuilder<TEntity> Post(string? path = null) => SetVerbShortcut(EntityActionKind.ApiCall, "POST", path);

    /// <summary>
    /// Verb shortcut: composes the URL as <c>PUT {RouteBase}/{id}/{path ?? actionName}</c>
    /// (no <c>{id}</c> when the action is pinned on the list header).
    /// Requires the entity to declare a <c>RouteBase</c>.
    /// </summary>
    public EntityActionBuilder<TEntity> Put(string? path = null) => SetVerbShortcut(EntityActionKind.ApiCall, "PUT", path);

    /// <summary>
    /// Verb shortcut: composes the URL as <c>DELETE {RouteBase}/{id}/{path ?? actionName}</c>
    /// (no <c>{id}</c> when the action is pinned on the list header).
    /// Requires the entity to declare a <c>RouteBase</c>.
    /// </summary>
    public EntityActionBuilder<TEntity> Delete(string? path = null) => SetVerbShortcut(EntityActionKind.ApiCall, "DELETE", path);

    /// <summary>
    /// Verb shortcut: composes the URL as <c>PATCH {RouteBase}/{id}/{path ?? actionName}</c>
    /// (no <c>{id}</c> when the action is pinned on the list header).
    /// Requires the entity to declare a <c>RouteBase</c>.
    /// </summary>
    public EntityActionBuilder<TEntity> Patch(string? path = null) => SetVerbShortcut(EntityActionKind.ApiCall, "PATCH", path);

    /// <summary>
    /// Verb shortcut: composes the URL as <c>GET {RouteBase}/{id}/{path ?? actionName}</c>
    /// (no <c>{id}</c> when the action is pinned on the list header).
    /// Requires the entity to declare a <c>RouteBase</c>. Returns JSON
    /// (<see cref="EntityActionKind.ApiCall"/>); use <see cref="Download"/> for
    /// binary streams.
    /// </summary>
    public EntityActionBuilder<TEntity> Get(string? path = null) => SetVerbShortcut(EntityActionKind.ApiCall, "GET", path);

    /// <summary>
    /// Bypasses <c>RouteBase</c> composition and forces an absolute URL after a
    /// verb shortcut has been applied. Useful when an action lives outside the
    /// entity's primary route prefix but still benefits from the verb shortcut's
    /// kind / method / confirmation chain. Equivalent in effect to using
    /// <see cref="ApiCall"/> with the same method and full URL.
    /// </summary>
    public EntityActionBuilder<TEntity> AbsolutePath(string urlTemplate)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(urlTemplate);
        _absolutePath = urlTemplate;
        return this;
    }

    private EntityActionBuilder<TEntity> SetVerbShortcut(EntityActionKind kind, string? method, string? path)
    {
        _kind = kind;
        _httpMethod = method;
        _useRouteBase = true;
        _pathSegment = path;
        _urlTemplate = null;
        return this;
    }

    /// <summary>
    /// Configures the action as a workflow transition. The renderer consults the
    /// entity's <c>WorkflowDefinitionType</c> to know whether the transition is
    /// allowed for the current row state — actions whose target state isn't
    /// reachable are rendered disabled.
    /// </summary>
    public EntityActionBuilder<TEntity> WorkflowTransition(string targetStateName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetStateName);

        _kind = EntityActionKind.WorkflowTransition;
        _httpMethod = null;
        _urlTemplate = null;
        _workflowTransitionName = targetStateName;
        return this;
    }

    /// <summary>
    /// Configures the action as a pure-frontend "open the side drawer" command.
    /// When <paramref name="urlTemplate"/> is <see langword="null"/> (default),
    /// the renderer opens the drawer on the entity's <c>details["default"]</c>
    /// layout for the row. Pass an explicit template only when the drawer must
    /// render a non-default detail surface or fetch a custom payload.
    /// </summary>
    public EntityActionBuilder<TEntity> OpenDrawer(string? urlTemplate = null)
    {
        _kind = EntityActionKind.OpenDrawer;
        _httpMethod = null;
        _urlTemplate = urlTemplate;
        return this;
    }

    /// <summary>
    /// Configures the action as a pure-frontend "open a modal dialog" command.
    /// When <paramref name="urlTemplate"/> is <see langword="null"/> (default),
    /// the renderer opens the modal on the entity's <c>forms["default"]</c>
    /// layout for the row (typical inline-edit case). Pass an explicit template
    /// for wizard-style modals (Import / Export, etc.).
    /// </summary>
    public EntityActionBuilder<TEntity> OpenModal(string? urlTemplate = null)
    {
        _kind = EntityActionKind.OpenModal;
        _httpMethod = null;
        _urlTemplate = urlTemplate;
        return this;
    }

    /// <summary>i18n key for the user-facing label.</summary>
    public EntityActionBuilder<TEntity> DisplayKey(string displayKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayKey);
        _displayKey = displayKey;
        return this;
    }

    /// <summary>Icon name from the framework's icon catalog.</summary>
    public EntityActionBuilder<TEntity> Icon(string icon)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(icon);
        _icon = icon;
        return this;
    }

    /// <summary>Display order among the entity's actions (lower first).</summary>
    public EntityActionBuilder<TEntity> Order(int order)
    {
        _order = order;
        return this;
    }

    /// <summary>
    /// Drops the action from the manifest payload when the user lacks this
    /// permission — defense in depth, never just hidden.
    /// </summary>
    public EntityActionBuilder<TEntity> RequiresPermission(string permissionName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(permissionName);
        _requiresPermission = permissionName;
        return this;
    }

    /// <summary>
    /// i18n key for a confirmation modal shown before invoking the action.
    /// Only meaningful for <see cref="EntityActionKind.ApiCall"/> and
    /// <see cref="EntityActionKind.WorkflowTransition"/>.
    /// </summary>
    public EntityActionBuilder<TEntity> Confirmation(string confirmationKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(confirmationKey);
        _confirmationKey = confirmationKey;
        return this;
    }

    /// <summary>
    /// Pins this action as a compact icon-button on the source entity's kanban
    /// tile (Phase 2.B.2). Use sparingly — kanban tiles have far less surface
    /// than the detail header, so only opt in for the actions the user genuinely
    /// performs at-a-glance (typical: quick "+ Note" / "+ Task" buttons or the
    /// most frequent lifecycle transition). The action is also rendered on the
    /// detail header via the same descriptor; the kanban tile reuses the same
    /// payload. Skip this for destructive or rare actions (Void, Archive).
    /// </summary>
    public EntityActionBuilder<TEntity> OnKanbanCard()
    {
        _showOnKanbanCard = true;
        return this;
    }

    /// <summary>
    /// Pins this action as a compact icon-button on the source entity's
    /// gallery card. Same surface-budget rationale as
    /// <see cref="OnKanbanCard"/> — gallery cards have less room than the
    /// detail header, so only opt in for the at-a-glance actions a user
    /// genuinely triggers from a card preview.
    /// </summary>
    public EntityActionBuilder<TEntity> OnGalleryCard()
    {
        _showOnGalleryCard = true;
        return this;
    }

    /// <summary>
    /// Pins this action as a compact icon-button on the source entity's
    /// calendar tile. Calendar tiles are smaller than kanban or gallery
    /// cards — curate even more carefully (typical use: a one-click
    /// "Join meeting" / "Mark done" on a per-event tile).
    /// </summary>
    public EntityActionBuilder<TEntity> OnCalendarTile()
    {
        _showOnCalendarTile = true;
        return this;
    }

    /// <summary>
    /// Pins this action on the list-page header (above the
    /// list / kanban / gallery / calendar tabs), not on individual rows.
    /// Use for entity-scope actions like Import, Export, BulkArchive —
    /// the URL template MUST NOT carry a <c>{id}</c> placeholder since no
    /// row is selected. Mirrors Odoo's top-of-list action bar.
    /// </summary>
    public EntityActionBuilder<TEntity> OnListHeader()
    {
        _showOnListHeader = true;
        return this;
    }

    internal EntityActionDescriptor Build()
    {
        string? resolvedUrl = ResolveUrl();

        // Kind-specific guards: invariants the fluent shortcuts can't catch on
        // their own (e.g. someone configures DisplayKey + Order without ever
        // calling ApiCall/Download/Navigate/WorkflowTransition/OpenDrawer/OpenModal).
        // OpenDrawer / OpenModal are pure-frontend kinds — a null URL is valid
        // (the renderer falls back to the entity's default detail / form layout).
        if (resolvedUrl is null
            && _kind is not EntityActionKind.WorkflowTransition
            && _kind is not EntityActionKind.OpenDrawer
            && _kind is not EntityActionKind.OpenModal)
        {
            throw new InvalidOperationException(
                $"Action '{_name}' must declare a URL via ApiCall(...) / Download(...) / Navigate(...), "
                + "a verb shortcut (Post/Put/Delete/Patch/Get) combined with .RouteBase(...), "
                + "be a WorkflowTransition, or use OpenDrawer() / OpenModal(). Bare actions are not allowed.");
        }

        // List-header actions are entity-scope — a {id} placeholder would
        // expand to nothing useful since no row is selected when the
        // header bar fires. Catch the misconfiguration at build time
        // (host startup) instead of letting it ship a broken URL.
        if (_showOnListHeader && resolvedUrl is { } template && template.Contains("{id}", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Action '{_name}' is pinned on the list header (OnListHeader) but its URL template contains '{{id}}'. "
                + "Header actions are entity-scope; remove the placeholder or surface this action on rows / cards instead.");
        }

        return new EntityActionDescriptor(
            Name: _name,
            Kind: _kind,
            DisplayKey: _displayKey,
            Icon: _icon,
            Order: _order,
            RequiresPermission: _requiresPermission,
            UrlTemplate: resolvedUrl,
            HttpMethod: _httpMethod,
            ConfirmationKey: _confirmationKey,
            WorkflowTransitionName: _workflowTransitionName,
            ContributorAssemblyName: _contributorAssemblyName,
            ShowOnKanbanCard: _showOnKanbanCard,
            ShowOnGalleryCard: _showOnGalleryCard,
            ShowOnCalendarTile: _showOnCalendarTile,
            ShowOnListHeader: _showOnListHeader);
    }

    private string? ResolveUrl()
    {
        // Precedence:
        //   1. .AbsolutePath(...)              → bypass everything
        //   2. .ApiCall(method, fullUrl)       → directly sets _urlTemplate
        //   3. Verb shortcut (Post / Get / …) → compose from RouteBase
        //   4. Legacy Download(fullUrl)        → arg is the full URL
        if (_absolutePath is not null)
        {
            return _absolutePath;
        }

        if (!_useRouteBase)
        {
            return _urlTemplate;
        }

        if (_routeBase is null)
        {
            // Legacy back-compat: pre-RouteBase callers wrote
            // .Download("/api/orders/{id}/pdf") with the full URL inline. Honour
            // that when no RouteBase is declared and a path was supplied.
            if (_kind == EntityActionKind.Download && _pathSegment is not null)
            {
                return _pathSegment;
            }

            throw new InvalidOperationException(
                $"Action '{_name}' uses a verb shortcut (Post/Put/Delete/Patch/Get/Download) but the entity declares no RouteBase. "
                + "Either declare .RouteBase(\"/api/...\") at the entity level or use .ApiCall(method, fullUrl) / .AbsolutePath(...) for this action.");
        }

        string trailing = _pathSegment ?? _name;
        string idSegment = _showOnListHeader ? string.Empty : "/{id}";
        string prefix = _routeBase.TrimEnd('/');
        return $"{prefix}{idSegment}/{trailing}";
    }
}
