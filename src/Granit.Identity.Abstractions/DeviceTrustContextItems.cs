namespace Granit.Identity;

/// <summary>
/// Well-known <see cref="System.Collections.Generic.IDictionary{TKey,TValue}">HttpContext.Items</see> keys
/// carrying the device-trust binding resolved from the signed cookie by the device-trust middleware.
/// </summary>
/// <remarks>
/// Lets a session-created emission site (BFF, OpenIddict) attach the stable device id to
/// <see cref="UserSessionCreatedEto"/> without referencing the HTTP/Data-Protection cookie machinery — it only
/// reads these string-keyed items (and verifies <see cref="UserId"/> matches the session's subject before
/// trusting <see cref="DeviceId"/>). Absent when no valid device cookie was presented.
/// </remarks>
public static class DeviceTrustContextItems
{
    /// <summary>Item key for the user id the device cookie is bound to (verify it matches before using the id).</summary>
    public const string UserId = "Granit:DeviceTrust:UserId";

    /// <summary>Item key for the stable device id the current browser is bound to.</summary>
    public const string DeviceId = "Granit:DeviceTrust:DeviceId";
}
