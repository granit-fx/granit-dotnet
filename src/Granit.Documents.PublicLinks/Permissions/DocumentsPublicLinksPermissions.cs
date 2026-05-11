namespace Granit.Documents.PublicLinks.Permissions;

/// <summary>
/// Permission constants exposed by <c>Granit.Documents.PublicLinks</c> —
/// gating creation, revocation and read-back of public sharing links.
/// </summary>
public static class DocumentsPublicLinksPermissions
{
    /// <summary>Permission group name (resource-key prefix for localisation).</summary>
    public const string GroupName = "DocumentsPublicLinks";

    /// <summary>Permissions on public sharing links.</summary>
    public static class PublicLinks
    {
        /// <summary>Create a new public link for a document.</summary>
        public const string Create = "DocumentsPublicLinks.PublicLinks.Create";

        /// <summary>Revoke an existing public link.</summary>
        public const string Revoke = "DocumentsPublicLinks.PublicLinks.Revoke";

        /// <summary>List / read public links attached to a document.</summary>
        public const string Read = "DocumentsPublicLinks.PublicLinks.Read";
    }
}
