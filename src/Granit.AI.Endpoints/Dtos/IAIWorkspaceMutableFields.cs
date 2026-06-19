namespace Granit.AI.Endpoints.Dtos;

/// <summary>
/// Shared contract for AI workspace Create and Update request payloads.
/// Enables reusable FluentValidation rules across both operations.
/// </summary>
public interface IAIWorkspaceMutableFields
{
    /// <summary>Provider identifier (e.g. <c>OpenAI</c>, <c>AzureOpenAI</c>).</summary>
    string Provider { get; }

    /// <summary>Model identifier (e.g. <c>gpt-4o</c>).</summary>
    string Model { get; }

    /// <summary>
    /// Human-readable display label (e.g. <c>GPT-4o</c>, <c>Support AI</c>).
    /// When <see langword="null"/>, the model identifier is used as the display fallback.
    /// </summary>
    string? WorkspaceModelName { get; }

    /// <summary>Optional system prompt.</summary>
    string? SystemPrompt { get; }

    /// <summary>Optional sampling temperature (0.0-2.0).</summary>
    float? Temperature { get; }

    /// <summary>Optional maximum output tokens.</summary>
    int? MaxOutputTokens { get; }
}
