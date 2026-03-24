using Granit.Modularity;

namespace Granit.Bff.Yarp;

/// <summary>
/// Granit module for BFF YARP reverse proxy integration.
/// Provides token injection, CSRF validation, and automatic silent refresh
/// for proxied requests.
/// </summary>
[DependsOn(typeof(GranitBffModule))]
public sealed class GranitBffYarpModule : GranitModule;
