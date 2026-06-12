using Granit.Modularity;

namespace Granit.IpGeolocation.IpInfo;

/// <summary>
/// Granit module for the opt-in ipinfo.io IP geolocation provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitIpGeolocationIpInfo()</c>. Because it sends the client IP to a
/// third-party processor, it only takes effect when explicitly registered and added to
/// <c>IpGeolocation:ProviderOrder</c>.
/// </remarks>
[DependsOn(typeof(GranitIpGeolocationModule))]
public sealed class GranitIpGeolocationIpInfoModule : GranitModule;
