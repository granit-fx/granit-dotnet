# Granit.Auditing.Privacy

Wolverine handlers for `Granit.Auditing`. Ships the `AuditingPrivacyDataProvider`
and a one-line `PersonalDataRequestedEto` handler that forwards to
`PrivacyFragmentUploader` — drop-in GDPR Art. 15 export of the user's audit
trail.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.Auditing.Privacy
```

## Dependencies

- `Granit.Auditing`
- `Granit.Privacy.BlobStorage`

## Usage

```csharp
[DependsOn(typeof(GranitAuditingPrivacyModule))]
public class MyAppModule : GranitModule { }
```

Opt-in on the privacy builder:

```csharp
services.AddGranitPrivacy(privacy => privacy
    .AddGranitAuditingPrivacyProvider());
```

## Fragment contents

The provider exports `auditing.json` containing the user's audit trail (entries
they authored), capped at 10 000 entries. The payload includes a `Truncated`
flag so downstream consumers can warn the user that some history was elided.

Shape:

```jsonc
{
  "userId": "...",
  "exportedEntries": 1234,
  "truncated": false,
  "limit": 10000,
  "entries": [
    {
      "id": "...",
      "timestamp": "2026-04-19T…",
      "category": "Authentication",
      "entityChanges": [{ "entityType": "…", "changeType": "Added", "propertyChanges": [] }]
    }
  ]
}
```

If the user has no audit entries, the provider returns an empty fragment and the
archive assembler records it under `manifest.EmptyProviders`.

## Documentation

See the [full documentation](https://granit-fx.dev).
