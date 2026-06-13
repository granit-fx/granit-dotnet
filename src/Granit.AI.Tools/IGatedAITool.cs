namespace Granit.AI.Tools;

/// <summary>
/// Optional companion to <see cref="IAITool"/> declaring a permission the caller must hold for the
/// tool to be offered (ADR-067). A tool that does not implement this interface is ungated and
/// always available (subject to its own data ACLs); a gated tool is filtered out of the agent's
/// available set for any user who lacks <see cref="RequiredPermission"/>, so admins can enable
/// capabilities tool by tool.
/// </summary>
public interface IGatedAITool
{
    /// <summary>
    /// The permission name (<c>[Group].[Resource].[Action]</c>) the caller must be granted, e.g.
    /// <c>AI.ChatTools.Translate</c>.
    /// </summary>
    string RequiredPermission { get; }
}
