namespace Granit.Identity.Endpoints.Dtos;

/// <summary>
/// Response for the identity provider capabilities endpoint.
/// </summary>
/// <param name="ProviderName">The display name of the active identity provider.</param>
/// <param name="SupportsIndividualSessionTermination">Whether the provider can terminate a specific session.</param>
/// <param name="SupportsNativePasswordResetEmail">Whether the provider can send a native password reset email.</param>
/// <param name="SupportsGroupHierarchy">Whether the provider supports hierarchical groups.</param>
/// <param name="SupportsCustomAttributes">Whether the provider supports custom user attributes.</param>
/// <param name="MaxCustomAttributes">Maximum number of custom attributes (0 if not supported).</param>
/// <param name="SupportsCredentialVerification">Whether the provider supports credential verification.</param>
/// <param name="SupportsUserCreation">Whether the provider supports user creation.</param>
/// <param name="SupportsGroupManagement">Whether tenant admins can create, update, and delete groups via the API.</param>
public sealed record IdentityProviderCapabilitiesResponse(
    string ProviderName,
    bool SupportsIndividualSessionTermination,
    bool SupportsNativePasswordResetEmail,
    bool SupportsGroupHierarchy,
    bool SupportsCustomAttributes,
    int MaxCustomAttributes,
    bool SupportsCredentialVerification,
    bool SupportsUserCreation,
    bool SupportsGroupManagement);
