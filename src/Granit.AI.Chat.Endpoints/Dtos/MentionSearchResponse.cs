namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>The candidates for the <c>@</c> picker, resolved under the caller's ACLs.</summary>
/// <param name="Items">The matching suggestions, capped at the requested limit.</param>
public sealed record MentionSearchResponse(IReadOnlyList<MentionSuggestionResponse> Items);

/// <summary>One <c>@</c> picker candidate. Selecting it yields a <c>MentionRequest(Type, Id)</c> on send.</summary>
/// <param name="Type">The mention type, matching an application-registered resolver.</param>
/// <param name="Id">The referenced entity's identifier, interpreted by the resolver for that type.</param>
/// <param name="Label">A concise human label for the row, e.g. <c>Ada Lovelace</c>.</param>
/// <param name="Description">An optional secondary line, e.g. an email, or <see langword="null"/>.</param>
public sealed record MentionSuggestionResponse(string Type, string Id, string Label, string? Description);
