namespace Granit.AI.Workspaces;

/// <summary>
/// An AI workspace: a named configuration binding a provider, model, and parameters.
/// </summary>
/// <remarks>
/// Workspaces can be <see cref="AIWorkspaceKind.System"/> (declared in code, immutable)
/// or <see cref="AIWorkspaceKind.Dynamic"/> (managed via API, persisted in database).
/// Each workspace is scoped to a tenant when multi-tenancy is active.
/// </remarks>
public sealed record AIWorkspace
{
    /// <summary>
    /// Unique name identifying this workspace (e.g. <c>support-chat</c>, <c>document-extraction</c>).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Provider identifier (e.g. <c>OpenAI</c>, <c>AzureOpenAI</c>, <c>Anthropic</c>, <c>Ollama</c>).
    /// </summary>
    public required string Provider { get; init; }

    /// <summary>
    /// Model identifier (e.g. <c>gpt-4o</c>, <c>claude-sonnet-4-6</c>, <c>llama3.2</c>).
    /// </summary>
    public required string Model { get; init; }

    /// <summary>
    /// Optional system prompt prepended to all conversations in this workspace.
    /// </summary>
    public string? SystemPrompt { get; init; }

    /// <summary>
    /// Sampling temperature (0.0–2.0). Lower values are more deterministic.
    /// </summary>
    public float? Temperature { get; init; }

    /// <summary>
    /// Maximum tokens to generate in a single response.
    /// </summary>
    public int? MaxOutputTokens { get; init; }

    /// <summary>
    /// Whether this workspace is system-defined (immutable) or dynamically managed.
    /// </summary>
    public AIWorkspaceKind Kind { get; init; } = AIWorkspaceKind.Dynamic;

    /// <summary>
    /// Tenant that owns this workspace, or <c>null</c> for global workspaces.
    /// </summary>
    public Guid? TenantId { get; init; }

    /// <summary>
    /// Whether this workspace is active and available for use.
    /// </summary>
    public bool Activated { get; init; } = true;
}
