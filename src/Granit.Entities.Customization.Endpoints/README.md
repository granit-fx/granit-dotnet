# Granit.Entities.Customization.Endpoints

Minimal API endpoints for `Granit.Entities.Customization` ([ADR-053][adr-053]).
Exposes the per-tenant Layer 1 customization on each `EntityDefinition` layout
through `GET` / `PUT` / `DELETE`, with mandatory ISO 27001 audit trail and a
single permission gate.

## What's inside

| Type | Role |
| ---- | ---- |
| `EntityCustomizationEndpoints` | `GET` / `PUT` / `DELETE` for `/{prefix}/{entityName}/customization/{layoutKind}` |
| `EntityCustomizationRequest` / `EntityCustomizationResponse` | Wire-shape DTOs; deltas serialized through the polymorphic `LayoutDelta` discriminator (`reorder` / `regroup` / `hide`) |
| `EntityCustomizationRequestValidator` | FluentValidation: shape-level (delta count cap, anchor well-formed, group key non-empty) |
| `DescriptorDeltaValidator` | Semantic validation against the live `EntityDefinitionDescriptor` (every `FieldName` resolves; reorder anchors resolve; regroup target group resolves) |
| `EntityCustomizationAuditWriter` | Emits an `IAuditingWriter` entry with category `ConfigurationChange` on every PUT / DELETE — captures the human-readable delta diff per ADR-053 §Audit trail |
| `EntitiesCustomizationPermissions` | `EntitiesCustomization.Customizations.Read` (group gate) and `EntitiesCustomization.Customizations.Manage` (PUT / DELETE) |
| `EntitiesCustomizationPermissionDefinitionProvider` | Auto-discovered by `GranitAuthorizationModule`; declares the two permissions in 18 cultures |
| `MapGranitEntitiesCustomization()` | Route-builder extension — applies the `Read` permission to the route group, `WithTags("Customization")` for OpenAPI |

## Endpoints

| Method | Route | Permission | Purpose |
| ------ | ----- | ---------- | ------- |
| `GET` | `/{prefix}/{entityName}/customization/{layoutKind}` | `Read` | Returns the persisted deltas, or 404 when the tenant has not customized that layout (compiled defaults apply) |
| `PUT` | `/{prefix}/{entityName}/customization/{layoutKind}` | `Manage` | Full-replace; validates the payload against the compiled descriptor; writes audit entry on success |
| `DELETE` | `/{prefix}/{entityName}/customization/{layoutKind}` | `Manage` | Reverts the tenant to compiled defaults; idempotent (returns 204 even when no row exists) |

`{layoutKind}` is the `LayoutKind` enum: `formDefault`, `detailDefault`, `list`, `calendar`, `gallery`.

## Validation pipeline

1. **FluentValidation auto-applied** by `MapGranitGroup()` — rejects malformed shapes (delta count over `MaxDeltasPerRequest`, reorder anchor not well-formed, empty `GroupKey`) with `400 ValidationProblem`.
2. **`DescriptorDeltaValidator`** — semantic validation against the compiled `EntityDefinitionDescriptor`. Rejects unknown `FieldName`s, dangling reorder anchors, and unknown regroup target groups with `400 Problem`.
3. **Endpoint** — calls `IEntityCustomizationWriter.UpsertAsync` then `EntityCustomizationAuditWriter.WriteUpsertAsync`.

For `LayoutKind.List` / `Calendar` / `Gallery` the descriptor exposes the compiled types only as `Type` references; semantic validation against those layouts is deferred to the manifest composer (story B4) which has the concrete descriptor instance. The endpoint accepts the deltas — the composer is the authoritative validator that silently drops overrides referencing a deleted field.

## Audit shape

Every PUT / DELETE writes one `AuditEntry` with:

- `Category = ConfigurationChange`
- `EntityType = "Granit.Entities.Customization:{entityName}"`
- `EntityId = "{layoutKind}"`
- `ChangeType = Created | Modified | Deleted`
- `PropertyChanges = [{ PropertyName: "Deltas", OriginalValue: <JSON>, NewValue: <JSON> }]`

Auditors and tenant admins replay the customization history without consulting a developer — the JSON shape is the same as the wire payload (polymorphic `$type` discriminator).

## Composition example

```csharp
// In the host's Program.cs
builder.AddGranitEntitiesCustomization()                          // B1 abstractions (Null reader by default)
       .AddGranitEntitiesCustomizationEntityFrameworkCore(opts =>  // B2 EF reader replaces Null
            opts.UseNpgsql(connectionString));

// Map endpoints under /api/v1/entities — group prefix is configurable.
app.MapGranitEntitiesCustomization();
```

## See also

- [ADR-053 — Layer 1 customization model][adr-053]
- `Granit.Entities.Customization` — abstractions package (B1)
- `Granit.Entities.Customization.EntityFrameworkCore` — EF companion (B2)
- `Granit.Entities.Endpoints` manifest composer (B4) — apply pipeline + provenance

[adr-053]: https://github.com/granit-fx/granit-dotnet/blob/develop/docs-site/src/content/docs/dotnet/architecture/adr/053-entities-customization-layer-1.md
