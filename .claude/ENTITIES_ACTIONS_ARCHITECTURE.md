# Granit.Entities - Action Architecture & Bulk Operations

**Last Updated:** May 2026  
**Scope:** Entity action system (ADR-040), bulk operation patterns, cross-module action contributions

---

## 1. Core Architecture Overview

The Granit entity action system follows a **cross-module contribution pattern** (per ADR-040) enabling modules to graft actions onto entities owned by other modules **without runtime dependencies**. Actions describe declarative UI surfaces (buttons, modals, workflows) and HTTP dispatch rules.

### Design Principles
- **Immutable Descriptors**: Actions are recorded as `EntityActionDescriptor` records at definition time
- **Lazy Contributor Resolution**: Contributors are discovered via DI enumerable after boot-time composition
- **Merge Strategy**: Intra-module actions take precedence; conflicts logged at debug level
- **Bulk Event Optimization**: `EntityBulkUpdatedEvent<T>` replaces N per-row events for cache invalidation efficiency

---

## 2. Core Interfaces & Implementations

### 2.1 IEntityActionContributor

**File**: [src/Granit.Entities.Abstractions/Actions/IEntityActionContributor.cs](src/Granit.Entities.Abstractions/Actions/IEntityActionContributor.cs)

```csharp
/// <summary>
/// Cross-module hook for grafting an action onto an entity owned by another module.
/// Same IoC contributor pattern as IEntityRelationContributor (per ADR-040).
/// Example: Granit.Tasks can graft an "add-task" quick-action onto Granit.Parties.Party
/// without either side taking a runtime dependency on the other.
/// </summary>
public interface IEntityActionContributor
{
    /// <summary>
    /// Apply this module's action contributions. Resolved after every
    /// EntityDefinition is registered; contributions targeting unknown
    /// source entities are dropped with a debug log.
    /// </summary>
    void Contribute(IEntityActionContributionContext context);
}

/// <summary>
/// Surface a contributor uses to graft actions onto a source entity.
/// </summary>
public interface IEntityActionContributionContext
{
    /// <summary>
    /// Adds an action to the source entity. Appears alongside intra-module
    /// actions in the manifest, ordered by EntityActionBuilder{TEntity}.Order.
    /// </summary>
    /// <param name="name">Stable action name, unique per source entity.</param>
    /// <param name="configure">Builder configuration delegate.</param>
    IEntityActionContributionContext AddAction<TSource>(
        string name,
        Action<EntityActionBuilder<TSource>> configure)
        where TSource : class;
}
```

**DI Registration**:
```csharp
// From EntityActionServiceCollectionExtensions.cs
services.AddSingleton<IEntityActionContributor, TContributor>();
```

---

### 2.2 EntityActionDescriptor

**File**: [src/Granit.Entities.Abstractions/Actions/EntityActionDescriptor.cs](src/Granit.Entities.Abstractions/Actions/EntityActionDescriptor.cs)

Immutable descriptor covering both intra-module and cross-module actions:

```csharp
public sealed record EntityActionDescriptor(
    string Name,                                    // e.g. "finalize", unique per entity
    EntityActionKind Kind,                          // ApiCall, Download, Navigate, WorkflowTransition, OpenDrawer, OpenModal
    string? DisplayKey,                             // i18n key for user-facing label
    string? Icon,                                   // Icon catalog name
    int Order,                                      // Display order (lower first)
    string? RequiresPermission,                     // Permission gate — defense-in-depth
    string? UrlTemplate,                            // URL with {id} placeholder for ApiCall/Download/Navigate
    string? HttpMethod,                             // POST/PUT/DELETE/GET for ApiCall
    string? ConfirmationKey,                        // i18n key for confirmation modal
    string? WorkflowTransitionName,                 // Target state for WorkflowTransition
    string? ContributorAssemblyName,                // Null for intra-module, assembly name for cross-module
    bool ShowOnKanbanCard = false,                  // Compact icon on kanban tile
    bool ShowOnGalleryCard = false,                 // Compact icon on gallery card
    bool ShowOnCalendarTile = false,                // Compact icon on calendar tile
    bool ShowOnListHeader = false,                  // Entity-scope action above list/kanban/gallery tabs (no {id})
    bool ShowOnSelection = false                    // Action on selection-bar dropdown (N parallel requests)
);
```

**Key Properties**:
- `ShowOnListHeader` → Entity-scope actions like `Import`, `Export`, `BulkArchive` (URL has **no** `{id}`)
- `ShowOnSelection` → Multi-row actions (renderer fires N parallel requests with `{id}` substituted)
- `ContributorAssemblyName` → Discriminates cross-module grafts from native declarations

---

### 2.3 EntityActionBuilder<TEntity>

**File**: [src/Granit.Entities.Abstractions/Actions/EntityActionBuilder.cs](src/Granit.Entities.Abstractions/Actions/EntityActionBuilder.cs)

Fluent builder ensuring consistent action descriptors:

```csharp
public sealed class EntityActionBuilder<TEntity> where TEntity : class
{
    // Kind shortcuts (each sets _kind, _httpMethod, _urlTemplate atomically)
    public EntityActionBuilder<TEntity> ApiCall(string method, string urlTemplate)
    public EntityActionBuilder<TEntity> Download(string? path = null)
    public EntityActionBuilder<TEntity> Navigate(string urlTemplate)
    public EntityActionBuilder<TEntity> WorkflowTransition(string targetStateName)
    public EntityActionBuilder<TEntity> OpenDrawer(string? urlTemplate = null)
    public EntityActionBuilder<TEntity> OpenModal(string? urlTemplate = null)

    // Verb shortcuts (compose URL as {RouteBase}/{id}/{path ?? actionName})
    public EntityActionBuilder<TEntity> Post(string? path = null)
    public EntityActionBuilder<TEntity> Put(string? path = null)
    public EntityActionBuilder<TEntity> Delete(string? path = null)
    public EntityActionBuilder<TEntity> Patch(string? path = null)
    public EntityActionBuilder<TEntity> Get(string? path = null)

    // URL override
    public EntityActionBuilder<TEntity> AbsolutePath(string urlTemplate)

    // Metadata
    public EntityActionBuilder<TEntity> DisplayKey(string displayKey)
    public EntityActionBuilder<TEntity> Icon(string icon)
    public EntityActionBuilder<TEntity> Order(int order)
    public EntityActionBuilder<TEntity> RequiresPermission(string permissionName)
    public EntityActionBuilder<TEntity> Confirmation(string confirmationKey)

    // Display surface
    public EntityActionBuilder<TEntity> OnKanbanCard()
    public EntityActionBuilder<TEntity> OnGalleryCard()
    public EntityActionBuilder<TEntity> OnCalendarTile()
    public EntityActionBuilder<TEntity> OnListHeader()
    public EntityActionBuilder<TEntity> OnSelection()

    // Build immutable descriptor
    internal EntityActionDescriptor Build()
}
```

**Example Usage** (intra-module):
```csharp
builder.Action("archive", a => a
    .Post("archive")                            // POST /api/parties/{id}/archive
    .DisplayKey("Entity:Party.Action.Archive")
    .Icon("archive")
    .Confirmation("Entity:Confirm.Archive")
    .RequiresPermission("Parties.Parties.Manage")
    .Order(10));
```

**Example Usage** (cross-module contribution):
```csharp
public class TasksEntityActionContributor : IEntityActionContributor
{
    public void Contribute(IEntityActionContributionContext context)
    {
        context.AddAction<Party>("add-task", a => a
            .Post("add-task")
            .DisplayKey("Task:Action.AddToParty")
            .Icon("plus")
            .OnListHeader()
            .Order(1));
    }
}
```

---

### 2.4 EntityActionContributionContext

**File**: [src/Granit.Entities.Abstractions/Actions/EntityActionContributionContext.cs](src/Granit.Entities.Abstractions/Actions/EntityActionContributionContext.cs)

Default implementation accumulating contributions in-memory:

```csharp
public sealed class EntityActionContributionContext : IEntityActionContributionContext
{
    private readonly Dictionary<Type, List<EntityActionDescriptor>> _bySource = [];

    /// <summary>The accumulated contributions, indexed by source CLR type.</summary>
    public IReadOnlyDictionary<Type, IReadOnlyList<EntityActionDescriptor>> Contributions =>
        _bySource.ToDictionary(
            kv => kv.Key,
            kv => (IReadOnlyList<EntityActionDescriptor>)kv.Value);

    public IEntityActionContributionContext AddAction<TSource>(
        string name,
        Action<EntityActionBuilder<TSource>> configure)
        where TSource : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        // Capture contributor assembly name
        Assembly contributorAssembly = configure.Method.DeclaringType?.Assembly
            ?? Assembly.GetCallingAssembly();
        string assemblyName = contributorAssembly.GetName().Name ?? "unknown";

        EntityActionBuilder<TSource> builder = new(name, assemblyName);
        configure(builder);

        if (!_bySource.TryGetValue(typeof(TSource), out List<EntityActionDescriptor>? bag))
        {
            bag = [];
            _bySource[typeof(TSource)] = bag;
        }
        bag.Add(builder.Build());

        return this;
    }
}
```

---

### 2.5 EntityActionKind Enum

**File**: [src/Granit.Entities.Abstractions/Actions/EntityActionKind.cs](src/Granit.Entities.Abstractions/Actions/EntityActionKind.cs)

Closed catalog of renderer-known action types:

```csharp
public enum EntityActionKind
{
    ApiCall = 0,              // HTTP write (POST/PUT/DELETE) + optional confirmation
    Download = 1,             // HTTP GET returning binary
    Navigate = 2,             // Client-side navigation (route or external URL)
    WorkflowTransition = 3,   // Workflow state change (renderer consults workflow definition)
    OpenDrawer = 4,           // Pure-frontend side drawer (optional custom URL)
    OpenModal = 5,            // Pure-frontend modal (optional custom URL, e.g. Import/Export wizards)
}
```

---

## 3. Composition & Merge Pipeline

### 3.1 EntityActionMerger

**File**: [src/Granit.Entities/Internal/EntityActionMerger.cs](src/Granit.Entities/Internal/EntityActionMerger.cs)

Boot-time merger folding contributed actions into entity definitions:

```csharp
internal static class EntityActionMerger
{
    public static IReadOnlyList<IEntityDefinitionDescriptor> Merge(
        IEnumerable<IEntityDefinitionDescriptor> definitions,
        IEnumerable<IEntityActionContributor> contributors,
        ILogger logger)
    {
        // 1. Collect all contributions
        EntityActionContributionContext context = new();
        foreach (IEntityActionContributor contributor in contributors)
        {
            contributor.Contribute(context);
        }

        // 2. Merge into host definitions
        Dictionary<Type, IEntityDefinitionDescriptor> byType = frozen.ToDictionary(d => d.EntityType);

        foreach ((Type sourceType, IReadOnlyList<EntityActionDescriptor> contributed) 
                 in context.Contributions)
        {
            if (!byType.TryGetValue(sourceType, out IEntityDefinitionDescriptor? hostDescriptor))
            {
                // Contributions targeting unknown entities → dropped with debug log
                logger.LogDebug(
                    "Entity action contribution targeted unknown source CLR type '{SourceType}' — dropped.",
                    sourceType.FullName);
                continue;
            }

            // Merge actions with conflict resolution
            EntityDefinitionDescriptor merged = MergeActions(
                hostDescriptor.Descriptor, 
                contributed, 
                logger);
            byType[sourceType] = new MergedActionsEntityDefinitionDescriptor(hostDescriptor, merged);
        }

        return [.. byType.Values];
    }

    private static EntityDefinitionDescriptor MergeActions(
        EntityDefinitionDescriptor host,
        IReadOnlyList<EntityActionDescriptor> contributed,
        ILogger logger)
    {
        // Intra-module actions take precedence
        var intraModuleNames = host.Actions
            .Where(a => a.ContributorAssemblyName is null)
            .Select(a => a.Name)
            .ToHashSet(StringComparer.Ordinal);

        List<EntityActionDescriptor> merged = [.. host.Actions];

        foreach (EntityActionDescriptor c in contributed)
        {
            if (intraModuleNames.Contains(c.Name))
            {
                logger.LogDebug(
                    "Cross-module action contribution '{Name}' on entity '{Entity}' was dropped " +
                    "because the source entity already declares an action with the same name.",
                    c.Name, host.Name);
                continue;
            }
            merged.Add(c);
        }

        // Final sort: by Order (ascending), then by Name (lexical)
        merged.Sort(static (a, b) =>
        {
            int byOrder = a.Order.CompareTo(b.Order);
            return byOrder != 0 ? byOrder : string.Compare(a.Name, b.Name, StringComparer.Ordinal);
        });

        return host with { Actions = merged };
    }
}
```

**Merge Rules**:
1. Unknown source entity → dropped with debug log
2. Action name conflict (intra-module vs cross-module) → intra-module wins, cross-module dropped
3. Final list sorted by `Order` (ascending) then `Name` (lexical)

---

### 3.2 EntityDefinitionRegistry Wiring

**File**: [src/Granit.Entities/Internal/EntityDefinitionRegistry.cs](src/Granit.Entities/Internal/EntityDefinitionRegistry.cs)

DI-orchestrated composition:

```csharp
internal sealed class EntityDefinitionRegistry : IEntityDefinitionRegistry
{
    public EntityDefinitionRegistry(
        IEnumerable<IEntityDefinitionDescriptor> definitions,
        IEnumerable<IEntityRelationContributor> relationContributors,
        IEnumerable<IEntityActionContributor> actionContributors,      // ← Discovered from DI
        ILogger<EntityDefinitionRegistry> logger)
    {
        // 1. Merge relations
        IReadOnlyList<IEntityDefinitionDescriptor> withRelations =
            EntityRelationMerger.Merge(definitions, relationContributors, logger);

        // 2. Merge actions
        IReadOnlyList<IEntityDefinitionDescriptor> merged =
            EntityActionMerger.Merge(withRelations, actionContributors, logger);

        // 3. Index by name and entity type
        // ...
    }
}
```

---

## 4. Intra-Module Action Declaration

### 4.1 EntityDefinitionBuilder<TEntity>

**File**: [src/Granit.Entities.Abstractions/EntityDefinitionBuilder.cs](src/Granit.Entities.Abstractions/EntityDefinitionBuilder.cs)

```csharp
public sealed class EntityDefinitionBuilder<TEntity> where TEntity : class
{
    private readonly List<EntityActionDescriptor> _actions = [];

    /// <summary>
    /// Declares an action exposed on this entity (button on detail header,
    /// row action, kanban tile quick-action — surface decided by the renderer).
    /// One of ApiCall, Download, Navigate, or WorkflowTransition must be called
    /// inside configure; otherwise Build() rejects the definition.
    /// </summary>
    public EntityDefinitionBuilder<TEntity> Action(
        string name,
        Action<EntityActionBuilder<TEntity>> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(configure);

        // RouteBase passed to builder for URL composition
        EntityActionBuilder<TEntity> builder = new(name, routeBase: _routeBase);
        configure(builder);
        _actions.Add(builder.Build());
        return this;
    }
}
```

**Usage Example**:
```csharp
public class PartyDefinition : EntityDefinition<Party>
{
    public override void Configure(EntityDefinitionBuilder<Party> builder)
    {
        builder
            .DisplayKey("Entity:Party")
            .Icon("contacts")
            .RouteBase("/api/parties")
            .Action("archive", a => a
                .Post("archive")
                .DisplayKey("Entity:Party.Action.Archive")
                .Icon("archive")
                .Confirmation("Entity:Confirm.Archive")
                .RequiresPermission("Parties.Parties.Manage")
                .Order(10))
            .Action("import", a => a
                .Post("import")
                .DisplayKey("Entity:Party.Action.Import")
                .Icon("upload")
                .OnListHeader()
                .Order(1));
    }
}
```

---

## 5. Current Endpoints & HTTP Dispatch

### 5.1 Entity Manifest Endpoints

**File**: [src/Granit.Entities.Endpoints/Endpoints/EntitiesEndpoints.cs](src/Granit.Entities.Endpoints/Endpoints/EntitiesEndpoints.cs)

```csharp
public static RouteGroupBuilder MapEntitiesEndpoints(this RouteGroupBuilder group)
{
    group.MapGet("/", DiscoveryAsync)
        .WithName("GetEntitiesDiscovery")
        .Produces<EntityDiscoveryResponse>();

    group.MapGet("/{name}", ManifestAsync)
        .WithName("GetEntityManifest")
        .Produces<EntityManifestResponse>()
        .ProducesProblem(StatusCodes.Status404NotFound)
        .ProducesProblem(StatusCodes.Status403Forbidden);

    return group;
}
```

**Manifest Payload Structure**:
```json
{
  "schemaVersion": "1.0",
  "entity": {
    "name": "Party",
    "displayKey": "Entity:Party",
    "actions": [
      {
        "name": "archive",
        "kind": 0,           // ApiCall
        "displayKey": "Entity:Party.Action.Archive",
        "icon": "archive",
        "httpMethod": "POST",
        "urlTemplate": "/api/parties/{id}/archive",
        "requiresPermission": "Parties.Parties.Manage",
        "showOnListHeader": false,
        "showOnSelection": false,
        "confirmationKey": "Entity:Confirm.Archive"
      },
      {
        "name": "import",
        "kind": 0,           // ApiCall
        "displayKey": "Entity:Party.Action.Import",
        "icon": "upload",
        "httpMethod": "POST",
        "urlTemplate": "/api/parties/import",
        "showOnListHeader": true,   // ← List header action (no {id})
        "showOnSelection": false
      }
    ]
  }
}
```

---

## 6. Bulk Operation Patterns

### 6.1 EntityBulkUpdatedEvent<T>

**File**: [src/Granit/Events/EntityBulkUpdatedEvent.cs](src/Granit/Events/EntityBulkUpdatedEvent.cs)

Domain event signaling batch operations:

```csharp
/// <summary>
/// Domain event signalling that a batch of TEntity rows was created,
/// updated, or deleted in a single host operation (story #1794).
/// 
/// Hosts emit this once per bulk operation instead of N per-row
/// EntityCreatedEvent / EntityUpdatedEvent / EntityDeletedEvent events
/// when the batch is large enough that fan-out invalidation would be wasteful
/// (e.g. a 100-row archive emitting 100 cache eviction calls when one would suffice).
/// </summary>
/// <typeparam name="TEntity">The entity type. Must implement IEmitEntityLifecycleEvents.</typeparam>
/// <param name="Entities">
/// The affected rows (post-save state for create/update; pre-delete snapshot for delete).
/// Empty list is allowed but a no-op for invalidation.
/// </param>
/// <remarks>
/// Consumed by RelationAggregateCacheInvalidator alongside per-row events:
/// a single bulk event drops the cached relation aggregates for every parent
/// of the relation in one RemoveByTagAsync call.
/// 
/// Hosts that emit this event SHOULD NOT also emit per-row events for the
/// same rows — the consumer treats them as alternatives, not complements.
/// </remarks>
public sealed record EntityBulkUpdatedEvent<TEntity>(IReadOnlyList<TEntity> Entities) : IDomainEvent
    where TEntity : class, IEmitEntityLifecycleEvents;
```

### 6.2 Bulk Event Cache Invalidation

**File**: [src/Granit.Entities.EntityFrameworkCore/Internal/RelationAggregateCacheInvalidator.cs](src/Granit.Entities.EntityFrameworkCore/Internal/RelationAggregateCacheInvalidator.cs)

Handles both per-row and bulk events:

```csharp
internal sealed class RelationAggregateCacheInvalidator<TRelated>(
    IFusionCache cache,
    RelationAggregateInvalidationTargets<TRelated> targets) :
    ILocalEventHandler<EntityCreatedEvent<TRelated>>,
    ILocalEventHandler<EntityUpdatedEvent<TRelated>>,
    ILocalEventHandler<EntityDeletedEvent<TRelated>>,
    ILocalEventHandler<EntityBulkUpdatedEvent<TRelated>>   // ← Bulk event support
    where TRelated : class, Granit.Domain.IEmitEntityLifecycleEvents
{
    public Task HandleAsync(EntityCreatedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    public Task HandleAsync(EntityUpdatedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    public Task HandleAsync(EntityDeletedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        EvictAsync(cancellationToken);

    // One bulk event collapses to one eviction sweep — coarser tag granularity
    // already groups per (source, relation), so the bulk event simply avoids the
    // N-way fan-out cost when the host has a batch of writes (story #1794).
    public Task HandleAsync(EntityBulkUpdatedEvent<TRelated> localEvent, CancellationToken cancellationToken = default) =>
        localEvent.Entities.Count == 0
            ? Task.CompletedTask
            : EvictAsync(cancellationToken);

    private async Task EvictAsync(CancellationToken cancellationToken)
    {
        foreach (string tag in targets.EvictionTags)
        {
            await cache.RemoveByTagAsync(tag, token: cancellationToken).ConfigureAwait(false);
        }
    }
}
```

### 6.3 Bulk Archive Pattern (ShowOnListHeader)

Actions declared with `.OnListHeader()` are entity-scope operations suitable for bulk workflows:

```csharp
// Pattern: bulk archive action on Parties entity
builder.Action("bulk-archive", a => a
    .Post("bulk-archive")
    .DisplayKey("Entity:Party.Action.BulkArchive")
    .Icon("archive-multiple")
    .OnListHeader()                    // ← Pinned above list/kanban/gallery/calendar
    .Order(20));

// Frontend renders: POST /api/parties/bulk-archive with selected row IDs in body
// Backend emits: EntityBulkUpdatedEvent<Party> with archived entities
```

---

## 7. File Structure Summary

### Abstractions (Public API)
| File | Purpose |
|------|---------|
| [Actions/IEntityActionContributor.cs](src/Granit.Entities.Abstractions/Actions/IEntityActionContributor.cs) | Cross-module contribution interface |
| [Actions/EntityActionDescriptor.cs](src/Granit.Entities.Abstractions/Actions/EntityActionDescriptor.cs) | Immutable action record |
| [Actions/EntityActionBuilder.cs](src/Granit.Entities.Abstractions/Actions/EntityActionBuilder.cs) | Fluent builder for actions |
| [Actions/EntityActionContributionContext.cs](src/Granit.Entities.Abstractions/Actions/EntityActionContributionContext.cs) | Default contribution context |
| [Actions/EntityActionKind.cs](src/Granit.Entities.Abstractions/Actions/EntityActionKind.cs) | Action type enum |
| [Actions/EntityActionServiceCollectionExtensions.cs](src/Granit.Entities.Abstractions/Actions/EntityActionServiceCollectionExtensions.cs) | DI registration helpers |
| [EntityDefinitionBuilder.cs](src/Granit.Entities.Abstractions/EntityDefinitionBuilder.cs) | Intra-module definition DSL |

### Implementation (Internal)
| File | Purpose |
|------|---------|
| [Internal/EntityActionMerger.cs](src/Granit.Entities/Internal/EntityActionMerger.cs) | Boot-time merge pipeline |
| [Internal/EntityDefinitionRegistry.cs](src/Granit.Entities/Internal/EntityDefinitionRegistry.cs) | Orchestrates relations + actions merge |
| [EntityFrameworkCore/RelationAggregateCacheInvalidator.cs](src/Granit.Entities.EntityFrameworkCore/Internal/RelationAggregateCacheInvalidator.cs) | Bulk event handler |

### Events
| File | Purpose |
|------|---------|
| [Events/EntityBulkUpdatedEvent.cs](src/Granit/Events/EntityBulkUpdatedEvent.cs) | Bulk operation event |

### Endpoints
| File | Purpose |
|------|---------|
| [Endpoints/EntitiesEndpoints.cs](src/Granit.Entities.Endpoints/Endpoints/EntitiesEndpoints.cs) | Manifest API handlers |

---

## 8. Key Design Patterns

### 8.1 Cross-Module Contribution (ADR-040)
**Problem**: Module A wants to add actions to entity owned by Module B without a direct reference.  
**Solution**: Module B discovers `IEntityActionContributor` implementations at boot via DI enumerable.  
**Benefit**: Loose coupling, no circular dependencies, order-independent registration.

### 8.2 Immutable Descriptors
All actions become `EntityActionDescriptor` records at definition time. Descriptors are:
- **Immutable** → frozen after boot
- **Serializable** → included in manifest JSON
- **Indexed** → registered in `EntityDefinitionDescriptor.Actions` list

### 8.3 Builder Pattern with Type Safety
`EntityActionBuilder<TEntity>` ensures:
- Each kind (ApiCall, Download, Navigate, etc.) sets discriminator + required fields atomically
- No way to construct an invalid descriptor (e.g., ApiCall without HTTP method)
- Fluent chaining enables readable DSL

### 8.4 Merge Strategy
1. **Intra-module actions** always win over cross-module grafts
2. **Unknown sources** are dropped with debug logs (non-fatal)
3. **Final sort** by Order (ascending), then Name (lexical) for deterministic rendering

### 8.5 Bulk Event Optimization (Story #1794)
Instead of emitting N per-row lifecycle events for large batch operations:
- Emit **one** `EntityBulkUpdatedEvent<T>` containing the full batch
- Cache invalidators handle bulk events **or** per-row events (mutually exclusive)
- Reduces fan-out cost from O(N) to O(1) for relation aggregate invalidation

---

## 9. Manifest Composition Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ App Startup                                                     │
└─────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────┐
│ DI Container Discovered                                         │
│ - IEntityDefinitionDescriptor[] (intra-module)                 │
│ - IEntityRelationContributor[] (cross-module relations)        │
│ - IEntityActionContributor[] (cross-module actions)            │
└─────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────┐
│ EntityDefinitionRegistry Constructor                            │
│   1. EntityRelationMerger.Merge(definitions, relationContribs) │
│   2. EntityActionMerger.Merge(result, actionContribs)          │
│   3. Index by name & entity type                               │
└─────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────┐
│ HTTP Request: GET /api/entities/Party                          │
│   1. IEntityDefinitionRegistry.GetByName("Party")              │
│   2. EntityManifestComposer composes response                  │
│   3. Actions serialized in manifest JSON                       │
└─────────────────────────────────────────────────────────────────┘
                                ↓
┌─────────────────────────────────────────────────────────────────┐
│ Frontend Renders Actions                                        │
│   - Detail header: all non-selection actions                   │
│   - Kanban tile: actions with ShowOnKanbanCard=true            │
│   - List header: actions with ShowOnListHeader=true            │
│   - Selection bar: actions with ShowOnSelection=true (N calls) │
└─────────────────────────────────────────────────────────────────┘
```

---

## 10. Existing Patterns & Conventions

### No Existing IEntityActionContributor Implementations
The framework **defines** the contribution interface but currently has **no** cross-module action implementations. This is the baseline for future bulk action patterns (e.g., Granit.Archiving contributing bulk-archive actions to various entities).

### Missing Infrastructure
**For Bulk Action Executor (Issue #1822)**:
- [ ] BulkActionExecutor: orchestrates batch operations (query filtered rows → apply action → emit bulk event)
- [ ] BulkActionRequest/Response DTOs for API contracts
- [ ] Endpoint: `POST /api/{entity}/actions/{action}/bulk` with selection criteria
- [ ] Permission checks: bulk actions inherit from entity's base permission
- [ ] Transactional guarantees & retry logic for large batches
- [ ] Background job support for long-running bulk operations

---

## 11. Next Steps for Issue #1822

### Recommended Architecture
1. **BulkActionExecutor Service** in base `Granit.Entities`
   - Accepts: entity type, action name, selection criteria (IDs or filter)
   - Applies action logic (built-in or delegated to module-specific handlers)
   - Emits `EntityBulkUpdatedEvent<T>` upon completion

2. **BulkActionContributor** Pattern (extends `IEntityActionContributor`)
   - Module declares which bulk actions it supports (e.g., `archive`, `delete`, `export`)
   - Executor delegates implementation to registered handler

3. **Endpoint Layer** (`Granit.Entities.Endpoints`)
   - `POST /api/{entity}/actions/{action}/bulk` — trigger bulk action
   - Query DSL integration: `?where=...` for row selection

4. **Transactionality & Batching**
   - Large batches (1000+) → split into background job chunks
   - Each chunk → separate `EntityBulkUpdatedEvent<T>` for cache correctness
   - Retry policy per chunk

---

## References

- **ADR-040**: Cross-module entity relations and actions (contributor pattern)
- **Story #1794**: Bulk event optimization for relation aggregate invalidation
- **Story #1822**: Bulk action executor (in-progress)
- **CLAUDE.md**: Framework conventions & module anatomy
