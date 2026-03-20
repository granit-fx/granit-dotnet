namespace Granit.AI.Endpoints.Permissions;

/// <summary>
/// Permission constants for AI administration and inference.
/// </summary>
public static class AIPermissions
{
    public const string GroupName = "AI";

    public static class Workspaces
    {
        public const string View = "AI.Workspaces.View";
        public const string Create = "AI.Workspaces.Create";
        public const string Update = "AI.Workspaces.Update";
        public const string Delete = "AI.Workspaces.Delete";
    }

    public static class Usage
    {
        public const string View = "AI.Usage.View";
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
