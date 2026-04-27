# Granit.Parties.Mergeable

Wires the [`Party`](../Granit.Parties/Domain/Party.cs) aggregate into the generic merge
orchestrator from [`Granit.Mergeable.EntityFrameworkCore`](../Granit.Mergeable.EntityFrameworkCore/).

## What it ships

| Type | Role |
|---|---|
| `PartyMergeableAggregateAdapter` *(internal)* | EF-backed `IMergeableAggregateAdapter<Party>` — `LoadAsync`, `PersistMergedPairAsync`, `ApplyTombstone`, `CollapseChainTombstonesAsync`. |
| `AddGranitPartiesMergeable()` | DI extension on `IHostApplicationBuilder` registering the adapter as `Scoped`. |
| `GranitPartiesMergeableModule` | Module marker, depends on `GranitMergeableEntityFrameworkCoreModule`. |

## What it does NOT ship

Cross-module reference rewriters (`Invoice.PartyId`, `Subscription.PartyId`,
`BalanceAccount.PartyId`) live in **their own** `*.Mergeable` packages — each module that
holds a `PartyId` is responsible for the SQL bulk-update on its own foreign key. See:

- `Granit.Invoicing.Mergeable` (story #1288)
- `Granit.Subscriptions.Mergeable` (story #1289)
- `Granit.CustomerBalance.Mergeable` (story #1290)
- `PartyChildrenReferenceRewriter` (Addresses / Emails / Phones / ExternalMappings) lives
  in `Granit.Parties.EntityFrameworkCore` (story #1287).

## Usage

```csharp
// In the host program (e.g. Showcase.Host) :
builder.AddGranitPartiesEntityFrameworkCore(opt => opt.UseNpgsql(connectionString));
builder.AddGranitMergeableEntityFrameworkCore(opt => opt.UseNpgsql(connectionString));
builder.AddGranitPartiesMergeable();

// Then the consumer can resolve and call :
var mergeService = sp.GetRequiredService<IMergeService<Party>>();
MergeResult<Party> preview = await mergeService.MergePreviewAsync(survivorId, loserId, ct);
MergeResult<Party> result = await mergeService.MergeAsync(
    new MergeRequest(survivorId, loserId, choices,
        Reason: "duplicate via CRM import",
        IdempotencyKey: "abc-123"),
    ct);
```

## Conflict resolution rules (recap from `Party.GetConflicts` + `Party.MergeFrom`)

| Field | Default winner | Override |
|---|---|---|
| `TenantId` / `Kind` / `DefaultCurrency` | **Hard invariants** — must match, throws `MergeException` otherwise. | None. |
| `Status` | Survivor must be `Active`, loser `Active` or `Suspended`. | None — `Archived` loser rejected. |
| `Name` / `Website` / `Language` / `Timezone` / `TaxId` / `RegistrationNumber` / `AvatarBlobId` / `ParentContactId` | Survivor wins. | Yes, per field path. |
| `Roles` (`[Flags]`) | **Always union** (bitwise OR) — non-overridable. Losing a flag is destructive. | None. |
| `TaxStatus` | Prefer non-`Standard` if one is `Standard`, otherwise survivor. | Yes (`"TaxStatus"`). |
| `UserId` | Transfer if survivor null + loser non-null, otherwise survivor. | Yes (`"UserId"`). |
| `Metadata` | Merge dictionaries, survivor wins on key conflict. | Yes (`"Metadata.<key>"`). |
| `InternalNotes` | Append `<survivor>\n\n--- merged from <loserId> ---\n<loser>`. | Yes (`"InternalNotes"`) → full replace. |
| Children (`Addresses` / `Emails` / `Phones` / `ExternalMappings`) | NOT touched in-memory ; reconciled by `PartyChildrenReferenceRewriter` (#1287). | N/A. |

## License

Apache 2.0.
