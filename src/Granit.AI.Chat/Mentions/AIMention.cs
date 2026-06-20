namespace Granit.AI.Chat.Mentions;

/// <summary>
/// A user <c>@</c> reference carried on a send request (ADR-067): a typed pointer to an
/// application entity the user wants the agent to consider for this turn. The server resolves
/// it to context under the caller's ACLs via the matching <see cref="Granit.Mentions.IMentionResolver"/>;
/// a mention the caller cannot see resolves to nothing and is never leaked into the prompt.
/// </summary>
/// <param name="Type">The mention type, matching an opted-in resolver's <see cref="Granit.Mentions.IMentionResolver.Type"/>.</param>
/// <param name="Id">The opaque entity identifier, interpreted by the resolver for that type.</param>
public sealed record AIMention(string Type, string Id);
