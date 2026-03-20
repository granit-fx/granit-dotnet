namespace Granit.AI.Endpoints.Options;

/// <summary>
/// Configuration options for the AI endpoints.
/// </summary>
public sealed class AIEndpointsOptions
{
    /// <summary>Configuration section name.</summary>
    public const string SectionName = "AIEndpoints";

    /// <summary>
    /// Route prefix for all AI endpoints.
    /// Default: <c>"ai"</c>.
    /// </summary>
    public string RoutePrefix { get; set; } = "ai";

    /// <summary>
    /// Role required to access workspace administration and usage endpoints.
    /// Default: <c>"granit-ai-admin"</c>.
    /// </summary>
    public string AdminRole { get; set; } = "granit-ai-admin";

    /// <summary>
    /// Role required to access chat completion and embedding proxy endpoints.
    /// Default: <c>"granit-ai-user"</c>.
    /// </summary>
    public string UserRole { get; set; } = "granit-ai-user";

    /// <summary>
    /// OpenAPI tag name for workspace management endpoints.
    /// Default: <c>"AI Workspaces"</c>.
    /// </summary>
    public string WorkspacesTagName { get; set; } = "AI Workspaces";

    /// <summary>
    /// OpenAPI tag name for usage tracking endpoints.
    /// Default: <c>"AI Usage"</c>.
    /// </summary>
    public string UsageTagName { get; set; } = "AI Usage";

    /// <summary>
    /// OpenAPI tag name for chat and embedding proxy endpoints.
    /// Default: <c>"AI Inference"</c>.
    /// </summary>
    public string InferenceTagName { get; set; } = "AI Inference";

    /// <summary>
    /// Maximum number of messages allowed per chat request.
    /// Default: <c>100</c>.
    /// </summary>
    public int MaxChatMessages { get; set; } = 100;

    /// <summary>
    /// Maximum number of text inputs allowed per embedding request.
    /// Default: <c>50</c>.
    /// </summary>
    public int MaxEmbeddingInputs { get; set; } = 50;
}
