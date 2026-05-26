namespace Granit.Privacy.Endpoints.Permissions;

/// <summary>
/// Permission constants for GDPR privacy endpoints.
/// Format: <c>{Group}.{Resource}.{Action}</c> (PascalCase, plural Resource).
/// </summary>
public static class PrivacyPermissions
{
    /// <summary>Permission group name.</summary>
    public const string GroupName = "Privacy";

    /// <summary>Permissions for personal data export (GDPR Art. 15/20).</summary>
    public static class Exports
    {
        /// <summary>Request a personal data export for one's own data (self-service).</summary>
        public const string Execute = "Privacy.Exports.Execute";

        /// <summary>
        /// Request a personal data export on behalf of another subject (admin DSR path).
        /// Constant is declared but the corresponding endpoint surface is gated to a
        /// future iteration — the framework's current export handlers refuse any
        /// request where caller ≠ subject regardless of which permission is granted.
        /// </summary>
        public const string OnBehalfOf = "Privacy.Exports.OnBehalfOf";
    }

    /// <summary>Permissions for personal data deletion (GDPR Art. 17).</summary>
    public static class Deletions
    {
        /// <summary>Request and cancel personal data erasure.</summary>
        public const string Execute = "Privacy.Deletions.Execute";
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

    /// <summary>Permissions for legal document version management (admin).</summary>
    public static class LegalDocuments
    {
        /// <summary>View legal document versions and history.</summary>
        public const string Read = "Privacy.LegalDocuments.Read";

        /// <summary>Create new legal document drafts.</summary>
        public const string Create = "Privacy.LegalDocuments.Create";

        /// <summary>Edit, publish, and archive legal documents.</summary>
        public const string Manage = "Privacy.LegalDocuments.Manage";
    }
}
