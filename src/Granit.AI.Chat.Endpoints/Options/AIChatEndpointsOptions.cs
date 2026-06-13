namespace Granit.AI.Chat.Endpoints.Options;

/// <summary>Configuration for the chat conversation endpoints.</summary>
public sealed class AIChatEndpointsOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "AI:Chat:Endpoints";

    /// <summary>Route prefix. Default: <c>conversations</c>.</summary>
    public string RoutePrefix { get; set; } = "conversations";

    /// <summary>OpenAPI tag (Title Case). Default: <c>AI - Conversations</c>.</summary>
    public string TagName { get; set; } = "AI - Conversations";
}
