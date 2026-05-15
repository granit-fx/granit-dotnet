namespace Granit.AI.Endpoints.Workspaces;

/// <summary>Feature name constants for the AI module (per ADR-057).</summary>
public static class AIFeatures
{
    /// <summary>AI workspaces dashboard — paired with <c>/ai/workspaces</c>.</summary>
    public const string Workspaces = "ai.workspaces";

    /// <summary>AI usage dashboard — paired with <c>/ai/usage</c>.</summary>
    public const string Usage = "ai.usage";
}
