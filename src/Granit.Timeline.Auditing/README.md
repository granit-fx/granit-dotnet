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

To make an audit entry interactive (reactions / threaded replies), the
client first calls `POST /timeline/{entityType}/{entityId}/anchor` with
`{ sourceKey: "auditing", sourceId: "<audit-guid>" }` — the server
materializes a deterministic v5 shadow row that becomes the FK target for
subsequent interactions.

## License

Apache-2.0
