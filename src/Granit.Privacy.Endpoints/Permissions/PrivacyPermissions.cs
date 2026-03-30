namespace Granit.Privacy.Endpoints.Permissions;

/// <summary>
/// Permission constants for GDPR privacy endpoints.
/// Format: <c>{Group}.{Resource}.{Action}</c> (three dot-separated segments).
/// </summary>
public static class PrivacyPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Privacy";

    /// <summary>Permissions for personal data export (GDPR Art. 15/20).</summary>
    public static class Export
    {
        /// <summary>Request a personal data export.</summary>
        public const string Execute = "Privacy.Export.Execute";
    }

    /// <summary>Permissions for personal data deletion (GDPR Art. 17).</summary>
    public static class Deletion
    {
        /// <summary>Request and cancel personal data erasure.</summary>
        public const string Execute = "Privacy.Deletion.Execute";
    }

    /// <summary>Permissions for processing purpose management.</summary>
    public static class Purposes
    {
        /// <summary>Read registered processing purposes.</summary>
        public const string Read = "Privacy.Purposes.Read";
    }

    /// <summary>Permissions for legal agreement consent management (GDPR Art. 7).</summary>
    public static class Agreements
    {
        /// <summary>View legal documents and consent status.</summary>
        public const string Read = "Privacy.Agreements.Read";

        /// <summary>Accept a legal agreement.</summary>
        public const string Create = "Privacy.Agreements.Create";
    }
}
