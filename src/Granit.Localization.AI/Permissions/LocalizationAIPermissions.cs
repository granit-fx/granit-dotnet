namespace Granit.Localization.AI.Permissions;

/// <summary>
/// Permission constants owned by <c>Granit.Localization.AI</c>. The translate chat tool
/// (ADR-067) registers under the shared <c>AI</c> permission group, so its action name keeps
/// the <c>AI.</c> prefix while ownership of the declaration lives with the module that ships
/// the tool.
/// </summary>
public static class LocalizationAIPermissions
{
    /// <summary>Name of the shared <c>AI</c> permission group these permissions register under.</summary>
    public const string GroupName = "AI";

    /// <summary>
    /// Per-tool permissions for agentic chat capability tools (ADR-067) contributed by
    /// <c>Granit.Localization.AI</c>.
    /// </summary>
    public static class ChatTools
    {
        /// <summary>Grants the right to use the <c>translate</c> chat tool.</summary>
        public const string Translate = "AI.ChatTools.Translate";
    }
}
