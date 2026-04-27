# Granit.Parties.Deduplication

Three-tier duplicate detection for the [Granit.Parties](../Granit.Parties/README.md)
aggregate. Pluggable into the admin merge wizard and the upcoming background scan
job (#1300) via `IPartyDuplicateDetector`.

| Tier | What it does | When |
| ---- | ------------ | ---- |
| 1 — Deterministic | Exact lookup on canonical email / phone / VAT (story #1297) | Always — Tier-1 hits short-circuit with confidence 1.0 |
| 2 — Trigram blocking | `pg_trgm` similarity on `lower(name)` via the GIST index (story #1298) | PostgreSQL only; no-op fallback on other providers |
| 3 — Weighted scoring | Token-set ratio name + last-name ratio + Levenshtein address + email partial + phone-suffix exact | Re-rank of the Tier-2 candidate set |

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Parties.Deduplication
```

## Setup

```csharp
[DependsOn(
    typeof(GranitPartiesEntityFrameworkCoreModule),
    typeof(GranitPartiesDeduplicationModule))]
public class AppModule : GranitModule { }
```

```csharp
builder.AddGranitPartiesEntityFrameworkCore(o => o.UseNpgsql(...));
builder.AddGranitPartiesDeduplication();
```

## Tunable thresholds

Bound from the `Granit:Parties:Deduplication` configuration section (defaults baked
in — research baseline from Salesforce / HubSpot / Dynamics 365):

```json
{
  "Granit": {
    "Parties": {
      "Deduplication": {
        "NameSimilarityThreshold": 0.7,
        "CompanySimilarityThreshold": 0.6
      }
    }
  }
}
```

## Dependencies

- `Granit.Parties.EntityFrameworkCore` — canonicalisers + `PartyDeduplicationOptions`
- `FuzzySharp` — token-set ratio + partial Levenshtein
- `SoftWx.Match` — normalised Levenshtein on addresses

## Documentation

See the [full documentation](https://granit-fx.dev).
