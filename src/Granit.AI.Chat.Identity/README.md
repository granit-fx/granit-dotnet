# Granit.AI.Chat.Identity

Wires `Granit.Identity` as an [`IAIMentionResolver`](../Granit.AI.Chat/Mentions/IAIMentionResolver.cs)
for `Granit.AI.Chat` (ADR-067). The chat `@` picker can search the identity directory, and a
`@user` mention resolves to that user's context — both under the caller's ACLs.

## Usage

Add the module to your host application:

```csharp
[DependsOn(typeof(GranitAIChatIdentityModule))]
public class MyAppModule : GranitModule { }
```

Or register the resolver directly:

```csharp
services.AddIdentityChatMentions();
```

This exposes the `user` mention type. The picker calls
`GET /conversations/mentions?q=<query>&type=user`, and on send the client carries the chosen
candidate as `MentionRequest("user", <userId>)`.

## How it works

`UserMentionResolver` delegates to the active
[`IIdentityUserReader`](../Granit.Identity/IIdentityUserReader.cs) (local or federated):

- **Search** → `GetUsersAsync(query, first: 0, max: limit)`, mapping each user to a suggestion
  (label = full name / username / email; description = email).
- **Resolve** → `GetUserAsync(id)`; an absent user returns `null`, so the mention is silently
  dropped and nothing leaks into the prompt.

Tenant ACL is implicit: `IIdentityUserReader` runs in the caller's scope. The mention search
endpoint additionally gates on `AIChat.Conversations.Send`. Applications that require a stricter,
non-discoverable directory can layer their own permission check in a custom resolver.

## Documentation

See the [full documentation](https://granit-fx.dev).
