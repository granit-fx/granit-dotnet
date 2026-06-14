namespace Granit.AI.Prompts.Endpoints.Permissions;

/// <summary>
/// Permission constants for the prompt-catalogue endpoints. Format <c>[Group].[Resource].[Action]</c>.
/// Reads return the caller's catalogue (system prompts plus their own); writes are scoped to the
/// caller's own prompts in addition to the permission gate (system prompts are read-only).
/// </summary>
public static class AIPromptsPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "AIPrompts";

    /// <summary>Permissions for the prompt-templates resource.</summary>
    public static class Templates
    {
        /// <summary>Browse the catalogue and read individual prompts.</summary>
        public const string Read = "AIPrompts.Templates.Read";

        /// <summary>Create, update, and customise one's own prompts.</summary>
        public const string Manage = "AIPrompts.Templates.Manage";

        /// <summary>Delete one's own prompts.</summary>
        public const string Delete = "AIPrompts.Templates.Delete";
    }
}
