namespace Granit.AI.Chat.Domain;

/// <summary>The reason a user flags a <see cref="Message"/>, chosen from a closed set.</summary>
public enum MessageReportCategory
{
    /// <summary>The answer is factually wrong or misleading.</summary>
    Inaccurate,

    /// <summary>The content is harmful, unsafe, or offensive.</summary>
    Harmful,

    /// <summary>Any other reason; the free-text reason carries the detail.</summary>
    Other,
}
