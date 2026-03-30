namespace Granit.Http.Cookies;

/// <summary>
/// Consent model information resolved for the current request context.
/// Provided by <see cref="ICookieConsentModelProvider"/> implementations.
/// </summary>
/// <param name="Mode">The consent model for the current tenant/jurisdiction.</param>
/// <param name="HonorGlobalPrivacyControl">Whether the GPC signal must be honored (CCPA requires this).</param>
public sealed record ConsentModelInfo(CookieConsentMode Mode, bool HonorGlobalPrivacyControl);
