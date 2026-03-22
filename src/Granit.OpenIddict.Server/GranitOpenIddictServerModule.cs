using Granit.Core.Modularity;

namespace Granit.OpenIddict.Server;

/// <summary>
/// Granit module that configures the OpenIddict OIDC server:
/// endpoint URIs, flows, signing keys, custom grant types, and ASP.NET Core integration.
/// </summary>
[DependsOn(typeof(GranitOpenIddictModule))]
public sealed class GranitOpenIddictServerModule : GranitModule;
