namespace Granit.AI.Tools;

/// <summary>
/// Optional companion to <see cref="IAITool"/>: extra, code-first usage guidance composed into
/// the orchestrator system prompt (ADR-067, "per-tool instructions"). Implement this when the
/// model-facing <see cref="IAITool.Description"/> is not enough to use the tool well — e.g. when
/// to prefer it, how to phrase arguments, or caveats about its results.
/// </summary>
/// <remarks>
/// These instructions are code-first (they live in the tool's source), not a tenant-editable
/// catalogue prompt. A tool that does not implement this interface contributes no extra guidance.
/// </remarks>
public interface IAIToolInstructions
{
    /// <summary>Orchestrator-facing usage guidance for this tool.</summary>
    string Instructions { get; }
}
