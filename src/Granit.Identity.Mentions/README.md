# Granit.Identity.Mentions

Wires `Granit.Identity` as an [`IMentionResolver`](../Granit.Mentions/IMentionResolver.cs)
for the domain-neutral `@` mention seam. The picker can search the identity directory and a `@user`
mention resolves to that user's context — both under the caller's ACLs.

## Usage

Add the module to your host application:

```csharp
[DependsOn(typeof(GranitIdentityMentionsModule))]
public class MyAppModule : GranitModule { }
```

Or register the resolver directly:

```csharp
services.AddIdentityMentions();
```

This exposes the `user` mention type through `Granit.Mentions`. The picker is served by
`Granit.DataLookup`: `GET /lookups/mentions?search=<q>&scope.type=user`. On selection the chosen
candidate is carried back as the composite value `user:<userId>`.

## How it works

`UserMentionResolver` delegates to the active
[`IIdentityUserReader`](../Granit.Identity/IIdentityUserReader.cs) (local or federated):

- **Search** → `GetUsersAsync(query, first: 0, max: limit)`, mapping each user to a suggestion
  (label = full name / username / email; description = email).
- **Resolve** → `GetUserAsync(id)`; an absent user returns `null`, so the mention is silently
  dropped and nothing leaks.

Coarse access is gated by `Identity.Users.Read` (the same gate as `GET /identity/users`), enforced
by the host's mention authorizer. Tenant isolation is implicit: `IIdentityUserReader` runs in the
caller's scope.

## Documentation

See the [full documentation](https://granit-fx.dev).
