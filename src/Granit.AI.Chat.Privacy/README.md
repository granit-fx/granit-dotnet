# Granit.AI.Chat.Privacy

Privacy integration for [`Granit.AI.Chat`](../Granit.AI.Chat) (ADR-067, GDPR). Conversations are
personal data, so this package makes them participate in the `Granit.Privacy` flows:

- **Take-out (Art. 15/20)** — `ConversationPrivacyDataProvider` exports the user's conversations
  (with messages) as a staged JSON fragment in the scatter-gather export saga.
- **Erasure (Art. 17)** — a personal-data deletion handler hard-deletes the subject's conversations,
  their messages, and any message reports (the user-entered report reason is free-text personal data)
  on account deletion (soft delete would leave message content recoverable).

Attachment *content* is transient and owned by the application's blob store, which contributes its
own provider; the chat module never persists it.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat.Privacy
```

## Usage

First wire the module into the host's module graph so its Wolverine export/erasure handlers are
scanned and the chat EF data manager is loaded:

```csharp
[DependsOn(typeof(GranitAIChatPrivacyModule))]
public class MyAppModule : GranitModule { }
```

Then opt-in on the privacy builder:

```csharp
services.AddGranitPrivacy(privacy => privacy.AddGranitAIChatPrivacyProvider());
```

The `[DependsOn]` is mandatory: without it the module's domain and persistence dependencies are
never loaded, causing runtime failures during privacy export.

## Dependencies

- `Granit.AI.Chat`
- `Granit.Privacy.BlobStorage`

## Documentation

See the [full documentation](https://granit-fx.dev).
