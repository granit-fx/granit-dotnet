namespace Granit.Privacy.Endpoints.Workspaces;

/// <summary>Feature name constants for the privacy module (per ADR-057).</summary>
public static class PrivacyFeatures
{
    /// <summary>Privacy purposes list — paired with <c>/privacy/purposes</c>.</summary>
    public const string Purposes = "privacy.purposes";

    /// <summary>Privacy agreements list — paired with <c>/privacy/agreements</c>.</summary>
    public const string Agreements = "privacy.agreements";
}
