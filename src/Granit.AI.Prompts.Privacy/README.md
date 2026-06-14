# Granit.AI.Prompts.Privacy

Privacy integration for [`Granit.AI.Prompts`](../Granit.AI.Prompts) (ADR-067, GDPR). A user's own
prompt templates are personal data, so this package makes them participate in the `Granit.Privacy`
flows:

- **Take-out (Art. 15/20)** — `PromptTemplatePrivacyDataProvider` exports the user's own prompts as a
  staged JSON fragment in the scatter-gather export saga.
- **Erasure (Art. 17)** — a personal-data deletion handler hard-deletes the subject's own prompts on
  account deletion (soft delete would leave the content recoverable).

Framework-seeded **system prompts** are owned by no user and are never exported or erased.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Prompts.Privacy
```

## Usage

```csharp
services.AddGranitPrivacy(p => p.AddGranitAIPromptsPrivacyProvider());
```

## Dependencies

- `Granit.AI.Prompts`
- `Granit.Privacy.BlobStorage`

## Documentation

See the [full documentation](https://granit-fx.dev).
