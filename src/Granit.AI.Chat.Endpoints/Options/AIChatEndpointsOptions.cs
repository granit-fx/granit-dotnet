namespace Granit.AI.Chat.Endpoints.Options;

/// <summary>Configuration for the chat conversation endpoints.</summary>
public sealed class AIChatEndpointsOptions
{
    /// <summary>Route prefix. Default: <c>conversations</c>.</summary>
    public string RoutePrefix { get; set; } = "conversations";

    /// <summary>OpenAPI tag (Title Case). Default: <c>AI - Conversations</c>.</summary>
    public string TagName { get; set; } = "AI - Conversations";
}
