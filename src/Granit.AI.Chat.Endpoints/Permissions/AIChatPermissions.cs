namespace Granit.AI.Chat.Endpoints.Permissions;

/// <summary>
/// Permission constants for chat conversation endpoints. Format <c>[Group].[Resource].[Action]</c>.
/// Owner-private resources still gate the action by permission; ownership is enforced in addition.
/// </summary>
public static class AIChatPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "AIChat";

    /// <summary>Permissions for the conversations resource.</summary>
    public static class Conversations
    {
        /// <summary>List and read one's own conversations.</summary>
        public const string Read = "AIChat.Conversations.Read";

        /// <summary>Send messages and receive answers in one's own conversations.</summary>
        public const string Send = "AIChat.Conversations.Send";

        /// <summary>Create and rename one's own conversations.</summary>
        public const string Manage = "AIChat.Conversations.Manage";

        /// <summary>Delete one's own conversations.</summary>
        public const string Delete = "AIChat.Conversations.Delete";
    }
}
