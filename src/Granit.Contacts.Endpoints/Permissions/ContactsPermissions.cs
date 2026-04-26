namespace Granit.Contacts.Endpoints.Permissions;

/// <summary>Permission constants for the contacts administration endpoints.</summary>
public static class ContactsPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Contacts";

    /// <summary>Permissions for the contact resource.</summary>
    public static class Contacts
    {
        /// <summary>Grants read access to contacts (list + by-id + by-external-id).</summary>
        public const string Read = "Contacts.Contacts.Read";

        /// <summary>
        /// Grants write access to contacts (create / update / lifecycle transitions / role
        /// flags / hierarchy / external mappings / addresses / emails / phones / user link).
        /// </summary>
        public const string Manage = "Contacts.Contacts.Manage";
    }
}
