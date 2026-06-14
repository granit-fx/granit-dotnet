namespace Granit.AI.Chat.Suggestions;

/// <summary>
/// The context handed to each <see cref="IAISuggestionProvider"/> for a turn: who is asking and
/// what they said. Providers also run under the caller's scoped services (current user, ACLs), so
/// most gap detection (e.g. "is a calendar connected?") needs only the ambient identity.
/// </summary>
/// <param name="OwnerId">The user the turn belongs to.</param>
/// <param name="Message">The user's message this turn, for intent-relevant suggestions.</param>
public sealed record AISuggestionContext(Guid OwnerId, string Message);
