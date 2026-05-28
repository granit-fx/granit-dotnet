# Granit.Timeline.Auditing

Bridge package: projects `Granit.Auditing` entries into the federated
`Granit.Timeline` activity stream as `External`-origin `SystemLog` entries.
Once registered, every audited mutation targeting an `ITimelined` entity
shows up in that entity's timeline — no per-module wiring required.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Timeline.Auditing
```

## Dependencies

- `Granit.Timeline`
- `Granit.Auditing`

## Usage

```csharp
[DependsOn(
    typeof(GranitTimelineEntityFrameworkCoreModule),
    typeof(GranitAuditingEntityFrameworkCoreModule),
    typeof(GranitTimelineAuditingModule))]
public class AppModule : GranitModule { }
```

The bridge contributes an `ITimelineSource` with key `"auditing"`. The
Timeline reader fans out to it on every `GET /timeline/{entityType}/{entityId}`
and merges the audit projection with native timeline entries under the
canonical sort.

### Parent–child aggregation

Audits on child entities surface in the parent aggregate's timeline once a
module registers an `IAuditChildResolver`. The resolver returns the audited
child ids for a given parent, and the bridge issues a single batch query
covering parent + every child in one round-trip.

```csharp
public sealed class CmsPageAuditChildResolver(CmsDbContext db) : IAuditChildResolver
{
    public async Task<IReadOnlyCollection<AuditChildScope>> ResolveAsync(
        string parentEntityType, string parentEntityId, CancellationToken ct)
    {
        if (parentEntityType != "Page" || !Guid.TryParse(parentEntityId, out var pageId))
            return [];

        string[] versions = await db.PageVersions
            .Where(v => v.PageId == pageId)
            .Select(v => v.Id.ToString())
            .ToArrayAsync(ct);

        return [new AuditChildScope("PageVersion", versions)];
    }
}

// Registration — auto-discovered as IEnumerable<IAuditChildResolver>.
services.TryAddEnumerable(
    ServiceDescriptor.Scoped<IAuditChildResolver, CmsPageAuditChildResolver>());
```

Hosts with no resolver registered keep the pre-existing behaviour: only
audit rows whose `(EntityType, EntityId)` matches the URL exactly are
returned, and the cached `GetByEntityAsync` fast path stays hot.

To make an audit entry interactive (reactions / threaded replies), the
client first calls `POST /timeline/{entityType}/{entityId}/anchor` with
`{ sourceKey: "auditing", sourceId: "<audit-guid>" }` — the server
materializes a deterministic v5 shadow row that becomes the FK target for
subsequent interactions.

## License

Apache-2.0
