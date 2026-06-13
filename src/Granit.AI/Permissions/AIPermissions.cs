namespace Granit.AI.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.AI</c>.
/// </summary>
public static class AIPermissions
{
    /// <summary>Permission group name used in <c>IPermissionDefinitionContext.AddGroup()</c>.</summary>
    public const string GroupName = "AI";

    /// <summary>Permissions for the AI credentials resource.</summary>
    public static class Credentials
    {
        /// <summary>
        /// Grants the right to write per-tenant / per-workspace AI credentials.
        /// Required (in addition to <c>Settings.{Tenant,Global}.Manage</c>) for any
        /// <c>ISettingManager.Set*Async</c> call where the setting name starts with
        /// <c>Granit.AI.</c>. Required for the workspace credentials endpoint.
        /// </summary>
        public const string Manage = "AI.Credentials.Manage";
    }

    /// <summary>
    /// Per-tool permissions for agentic chat capability tools (ADR-067). A gated tool is offered
    /// to a user only when they hold the matching permission, so admins enable capabilities tool
    /// by tool. The action segment is the capability name (a domain-specific action).
    /// </summary>
    public static class ChatTools
    {
        /// <summary>Grants the right to use the <c>translate</c> chat tool (Localization.AI).</summary>
        public const string Translate = "AI.ChatTools.Translate";
    }
}
