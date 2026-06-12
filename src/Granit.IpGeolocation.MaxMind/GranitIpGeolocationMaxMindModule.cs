using Granit.Modularity;

namespace Granit.IpGeolocation.MaxMind;

/// <summary>
/// Granit module for the offline MaxMind (<c>.mmdb</c>) IP geolocation provider.
/// </summary>
/// <remarks>
/// Registration is done via <c>AddGranitIpGeolocationMaxMind()</c>. Add <c>"MaxMind"</c> to
/// <c>IpGeolocation:ProviderOrder</c> to activate it in the resolver fallback chain.
/// </remarks>
[DependsOn(typeof(GranitIpGeolocationModule))]
public sealed class GranitIpGeolocationMaxMindModule : GranitModule;
