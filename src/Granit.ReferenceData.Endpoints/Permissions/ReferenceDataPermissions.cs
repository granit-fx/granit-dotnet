namespace Granit.ReferenceData.Endpoints.Permissions;

/// <summary>
/// Permission constants for reference data administration.
/// </summary>
public static class ReferenceDataPermissions
{
    public const string GroupName = "ReferenceData";

    public static class Entries
    {
        public const string Read = "ReferenceData.Entries.Read";
        public const string Create = "ReferenceData.Entries.Create";
        public const string Manage = "ReferenceData.Entries.Manage";
    }
}
