# Granit.AI.Chat

Conversational AI for the Granit framework (ADR-067): the `Conversation` / `Message` aggregates
(multi-tenant, **private to their owner**) and the `IConversationStore` abstraction. Persistence
lives in `Granit.AI.Chat.EntityFrameworkCore`; the HTTP surface in `Granit.AI.Chat.Endpoints`.

Part of the [granit](https://granit-fx.dev) framework.

## Installation

```bash
dotnet add package Granit.AI.Chat
```

## Dependencies

- `Granit`

## `@` mentions

A turn can carry `@` mentions — typed references to application entities the user wants the
agent to consider. Resolution reuses the same opt-in, ACL-bound model as tools: register a
resolver per mention type, resolved per scope so it runs under the caller's identity.

```csharp
services.AddGranitChatMentions(mentions => mentions.Add<InvoiceMentionResolver>());

internal sealed class InvoiceMentionResolver(IInvoiceReader reader, ICurrentUser user) : IAIMentionResolver
{
    public string Type => "invoice";

    public async ValueTask<AIMentionContext?> ResolveAsync(string id, CancellationToken ct = default)
    {
        // Return null when absent OR the caller may not see it — the mention is then dropped,
        // never leaked into the prompt. Resolved content is wrapped as untrusted data.
        Invoice? invoice = await reader.FindForCallerAsync(id, ct);
        return invoice is null ? null : new AIMentionContext
        {
            Type = Type, Id = id, Label = $"Invoice #{invoice.Number}", Content = invoice.ToSummary(),
        };
    }
}
```

## File attachments

A turn can carry file attachments. v1 uses the model-agnostic **text-extraction** path: the
framework resolves each attachment's bytes via an application seam, extracts text
(`Granit.TextExtraction`), and injects it as untrusted context. Storage (transient,
conversation-scoped blob storage) is the application's concern, decoupled from the AI path.

```csharp
services.AddGranitChatAttachments<BlobAttachmentSource>(o => o.MaxAttachmentBytes = 5 * 1024 * 1024);

internal sealed class BlobAttachmentSource(IBlobStore store, ICurrentUser user) : IAIAttachmentSource
{
    public async Task<AIAttachmentData?> GetAsync(string reference, CancellationToken ct = default)
    {
        // Return null when absent OR the caller may not read it — the attachment is then dropped.
        Blob? blob = await store.FindForCallerAsync(reference, ct);
        return blob is null ? null : new AIAttachmentData(blob.Bytes, blob.ContentType, blob.FileName);
    }
}
```

Type/size limits (`AI:Chat:Attachments`) are enforced at the endpoint (request validation) and
re-checked against the resolved bytes server-side.

## Per-user settings

Three per-user settings (declared on the `Granit.Settings` `"U"` scope, read/written through the
generic settings endpoints) tune the chat per user:

| Setting | Effect |
| ------- | ------ |
| `Granit.AI.Chat.DefaultWorkspace` | Default chat workspace, or `Auto` to fall back to the configured default. Selectable list served by `GET {prefix}/workspaces` (chat-capable only). |
| `Granit.AI.Chat.WebSearchPolicy` | `Deny` / `Allow` / `AlwaysAsk` (the web-search provider is phase 2). |
| `Granit.AI.Chat.CustomContext` | Free text (≤ 4000 chars) layered into the system prompt below the framework guardrails. |

The custom context never overrides the guardrails; it only refines behaviour.

## Suggested actions

The agent can surface typed, **non-executing** call-to-actions (deep links) alongside its answer —
e.g. "connect a calendar" when none is linked. A module contributes a provider that detects its own
gaps under the caller's ACLs; the framework gathers them onto the response (a `suggestions` SSE
frame). v1 never executes a suggestion — it is display data plus a destination only.

```csharp
services.AddGranitChatSuggestions(s => s.Add<CalendarSuggestionProvider>());

internal sealed class CalendarSuggestionProvider(ICalendarReader calendars, ICurrentUser user) : IAISuggestionProvider
{
    public async ValueTask<IReadOnlyList<AISuggestedAction>> GetSuggestionsAsync(
        AISuggestionContext context, CancellationToken ct = default)
    {
        if (await calendars.IsConnectedAsync(ct))
        {
            return [];
        }

        return [new AISuggestedAction
        {
            Type = "calendar.connect",
            Label = "Connect a calendar",
            DeepLink = "/settings/integrations/calendar",
        }];
    }
}
```

## Documentation

See the [full documentation](https://granit-fx.dev).
