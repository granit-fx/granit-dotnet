namespace Granit.AI.Tools;

/// <summary>
/// The outcome of an <see cref="IAITool.InvokeAsync"/> call, fed back to the model on the
/// next turn of the loop.
/// </summary>
public sealed record AIToolResult
{
    /// <summary>
    /// The textual content returned to the model. For an error, this is a model-facing
    /// explanation it can recover from (it must never leak internal detail or data the
    /// caller is not authorized to see).
    /// </summary>
    public required string Content { get; init; }

    /// <summary>
    /// <see langword="true"/> when the call failed and <see cref="Content"/> describes the
    /// failure rather than a result.
    /// </summary>
    public bool IsError { get; init; }

    /// <summary>Creates a successful result carrying <paramref name="content"/>.</summary>
    public static AIToolResult Success(string content) => new() { Content = content };

    /// <summary>Creates an error result carrying a model-facing <paramref name="message"/>.</summary>
    public static AIToolResult Error(string message) => new() { Content = message, IsError = true };
}
