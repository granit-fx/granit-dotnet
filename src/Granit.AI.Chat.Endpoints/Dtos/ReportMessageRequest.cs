using Granit.AI.Chat.Domain;

namespace Granit.AI.Chat.Endpoints.Dtos;

/// <summary>Request to report (flag) a message for review.</summary>
/// <param name="Reason">The free-text reason the user supplies. Required, bounded length.</param>
/// <param name="Category">The optional category the user selects from a closed set.</param>
public sealed record ReportMessageRequest(string Reason, MessageReportCategory? Category = null);
