namespace Granit.AI.Prompts.Endpoints.Options;

/// <summary>Configuration for the prompt-catalogue endpoints.</summary>
public sealed class AIPromptsEndpointsOptions
{
    /// <summary>Configuration section path.</summary>
    public const string SectionName = "AI:Prompts:Endpoints";

    /// <summary>Route prefix. Default: <c>prompts</c>.</summary>
    public string RoutePrefix { get; set; } = "prompts";

    /// <summary>OpenAPI tag (Title Case). Default: <c>AI - Prompts</c>.</summary>
    public string TagName { get; set; } = "AI - Prompts";
}
