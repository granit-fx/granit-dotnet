namespace Granit.AI.Chat.Domain;

/// <summary>The author of a chat <see cref="Message"/>.</summary>
public enum MessageRole
{
    /// <summary>A message from the end user.</summary>
    User,

    /// <summary>A message from the assistant.</summary>
    Assistant,

    /// <summary>A system/instruction message.</summary>
    System,

    /// <summary>A tool result fed back into the conversation.</summary>
    Tool,
}
