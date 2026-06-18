namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>Request to send a message and receive a streamed answer.</summary>
/// <param name="Message">The user's message.</param>
/// <param name="ConversationId">The conversation to continue, or <see langword="null"/> to start a new one.</param>
/// <param name="WorkspaceName">The chat-capable workspace to use, or <see langword="null"/> for the default.</param>
/// <param name="Mentions">Entities <c>@</c>-referenced for this turn, resolved to context under the caller's ACLs.</param>
/// <param name="Attachments">Files attached to this turn, resolved to extracted text under the caller's ACLs.</param>
/// <param name="PromptRefs">Catalogue prompts inserted as <c>/</c> badges, composed with the message under the caller's ACLs.</param>
public sealed record SendMessageRequest(
    string Message,
    Guid? ConversationId = null,
    string? WorkspaceName = null,
    IReadOnlyList<MentionRequest>? Mentions = null,
    IReadOnlyList<AttachmentRequest>? Attachments = null,
    IReadOnlyList<Guid>? PromptRefs = null);

/// <summary>An <c>@</c> mention on a send: a typed reference to an application entity.</summary>
/// <param name="Type">The mention type, matching an application-registered resolver.</param>
/// <param name="Id">The referenced entity's identifier, interpreted by the resolver for that type.</param>
public sealed record MentionRequest(string Type, string Id);

/// <summary>A file attached to a send, resolved server-side to extracted text.</summary>
/// <param name="Reference">The opaque attachment identifier, resolved by the application's attachment source.</param>
/// <param name="FileName">The original file name.</param>
/// <param name="ContentType">The declared MIME type, validated against the allowed-type list.</param>
/// <param name="SizeBytes">The declared size in bytes, validated against the size limit.</param>
public sealed record AttachmentRequest(string Reference, string FileName, string ContentType, long SizeBytes);

/// <summary>
/// A frame streamed over SSE for a send. <see cref="Type"/> discriminates the frame:
/// <c>conversation</c> (carries <see cref="ConversationId"/>), <c>delta</c> (carries a
/// <see cref="Content"/> chunk), <c>tool_call</c> (a tool started — carries <see cref="ToolName"/> +
/// <see cref="ToolCallId"/>), <c>tool_result</c> (a tool finished — adds <see cref="Succeeded"/>),
/// <c>usage</c> (carries the token counts), <c>suggestions</c> (carries the typed
/// <see cref="SuggestedActions"/>), <c>clarification</c>, or <c>error</c> (a mid-stream failure —
/// carries <see cref="Code"/>).
/// </summary>
/// <remarks>
/// <para>
/// Tool frames carry only the tool's name and call id — never its arguments or raw result (privacy);
/// the front maps the name to a localized label. A "thinking" indicator is derived front-side from a
/// <c>tool_result</c> not yet followed by a <c>delta</c>, so there is no thinking frame on the wire.
/// </para>
/// <para>
/// An <c>error</c> frame is the terminal frame when the agentic loop fails <em>after</em> the stream
/// has been committed (a HTTP problem is no longer possible at that point — see the send endpoint,
/// which validates and returns 401/404/422/429 <em>before</em> opening the stream). It carries only
/// <see cref="Code"/>; <see cref="Content"/> stays <see langword="null"/> so no raw provider detail
/// leaks. The front maps the machine code to a localized message and never trusts backend text. A
/// client-initiated cancellation is <em>not</em> an error and produces no <c>error</c> frame.
/// </para>
/// <para>
/// <see cref="Code"/> is a stable, machine-readable code (<c>snake_case</c>) present only on the
/// <c>error</c> frame. The closed set is: <c>rate_limit</c> (provider quota exhausted / 429 mid-stream
/// — "denial of wallet"), <c>provider_unavailable</c> (provider 5xx, timeout, or connection failure),
/// and <c>server_error</c> (any other, unclassified failure — the default).
/// </para>
/// </remarks>
public sealed record ChatStreamEvent(
    string Type,
    string? Content = null,
    Guid? ConversationId = null,
    int? InputTokens = null,
    int? OutputTokens = null,
    IReadOnlyList<SuggestedActionResponse>? SuggestedActions = null,
    ClarificationResponse? Clarification = null,
    string? ToolName = null,
    string? ToolCallId = null,
    bool? Succeeded = null,
    string? Code = null);

/// <summary>A typed clarification the front renders as clickable choices; the turn blocks until answered.</summary>
/// <param name="Question">The disambiguating question.</param>
/// <param name="Options">The discrete choices.</param>
/// <param name="AllowOther">Whether to offer an "Other (describe)" free-text affordance.</param>
public sealed record ClarificationResponse(string Question, IReadOnlyList<ClarificationOptionResponse> Options, bool AllowOther);

/// <summary>One clickable choice of a <see cref="ClarificationResponse"/>.</summary>
/// <param name="Label">The display label.</param>
/// <param name="Value">The value sent back when chosen (falls back to <paramref name="Label"/> when null).</param>
public sealed record ClarificationOptionResponse(string Label, string? Value);

/// <summary>A typed, non-executing suggested action (a deep link the front renders).</summary>
/// <param name="Type">The suggestion type, e.g. <c>calendar.connect</c>.</param>
/// <param name="Label">The display label for the call-to-action.</param>
/// <param name="DeepLink">The deep link the front navigates to. Never auto-invoked.</param>
/// <param name="Description">An optional one-line explanation of the suggestion.</param>
public sealed record SuggestedActionResponse(string Type, string Label, string DeepLink, string? Description = null);
