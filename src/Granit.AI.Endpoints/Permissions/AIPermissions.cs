namespace Granit.AI.Endpoints.Permissions;

/// <summary>
/// Permission constants for AI administration and inference.
/// </summary>
public static class AIPermissions
{
    public const string GroupName = "AI";

    public static class Workspaces
    {
        public const string Read = "AI.Workspaces.Read";

        /// <summary>Grants management access to AI workspaces (create, update, delete).</summary>
        public const string Manage = "AI.Workspaces.Manage";
    }

    public static class Usage
    {
        public const string Read = "AI.Usage.Read";
    }

    public static class Chat
    {
        public const string Execute = "AI.Chat.Execute";
    }

    public static class Embeddings
    {
        public const string Execute = "AI.Embeddings.Execute";
    }
}
