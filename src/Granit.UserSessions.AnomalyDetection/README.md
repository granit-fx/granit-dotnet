# Granit.UserSessions.AnomalyDetection

Opt-in session anomaly detection for `Granit.UserSessions`. Classifies a session's risk
(`None`/`Low`/`Medium`/`High`) from always-on deterministic heuristics plus an optional AI
layer, persists the verdict to `IUserSessionRiskStore`, and raises an event for high risk.

Part of the [granit](https://granit-fx.dev) framework.

## How it works

`IUserSessionRiskEvaluator` is the entry point — consumers (the BFF login flow, the identity
authority) call it when a session is established, passing the new session plus the user's
session history.

1. **Heuristics (always on):** impossible travel (haversine distance ÷ time vs a max plausible
   speed), unfamiliar country, unfamiliar device family.
2. **AI (opt-in, `UseAi`):** `Granit.AI` `IStructuredCompletion` assesses the same coarse
   features and the two verdicts are merged (highest wins). The AI is fed **only** city/country,
   device family, and timestamps — **never the raw IP** — and degrades gracefully (rate limit,
   timeout, refusal) back to the heuristic verdict.

A non-`None` verdict is written to `IUserSessionRiskStore`; `Medium`/`High` also raises a
`SuspiciousUserSessionDetectedEto` (when a distributed event bus is present) for notifications
or step-up authentication.

## Registration

```csharp
// Program.cs — module auto-registers the detector + evaluator.
```

```json
{
  "UserSessions": {
    "AnomalyDetection": {
      "UseAi": false,
      "WorkspaceName": "default",
      "MaxTravelKilometersPerHour": 1000
    }
  }
}
```

| Key | Default | Description |
| --- | ------- | ----------- |
| `UseAi` | `false` | Enable the AI assessment layer (opt-in) |
| `WorkspaceName` | *(default workspace)* | AI workspace |
| `MaxAiCallsPerHourPerTenant` | `500` | AI call cap per tenant/hour |
| `AiTimeoutSeconds` | `15` | Per-call AI timeout |
| `MaxTravelKilometersPerHour` | `1000` | Impossible-travel speed threshold |

## Privacy (GDPR)

The AI layer never receives the raw IP — only coarse, derived features. Enable `UseAi` only when
sending those features to your configured model provider is approved.

## Dependencies

- `Granit.AI` (`IStructuredCompletion`)
- `Granit.UserSessions.Abstractions`

## License

Apache-2.0
