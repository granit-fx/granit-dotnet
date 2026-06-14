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

## Documentation

See the [full documentation](https://granit-fx.dev).
