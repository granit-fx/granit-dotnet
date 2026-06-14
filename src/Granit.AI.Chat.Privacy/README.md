# Granit.AI.Chat.Privacy

Privacy integration for [`Granit.AI.Chat`](../Granit.AI.Chat) (ADR-067, GDPR). Conversations are
personal data, so this package makes them participate in the `Granit.Privacy` flows:

- **Take-out (Art. 15/20)** — `ConversationPrivacyDataProvider` exports the user's conversations
  (with messages) as a staged JSON fragment in the scatter-gather export saga.
- **Erasure (Art. 17)** — a personal-data deletion handler hard-deletes the subject's conversations
  on account deletion (soft delete would leave message content recoverable).

Attachment *content* is transient and owned by the application's blob store, which contributes its
own provider; the chat module never persists it.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat.Privacy
```

## Usage

```csharp
services.AddGranitPrivacy(p => p.AddGranitAIChatPrivacyProvider());
```

## Dependencies

- `Granit.AI.Chat`
- `Granit.Privacy.BlobStorage`

## Documentation

See the [full documentation](https://granit-fx.dev).
