using Granit.Modularity;
using Granit.MultiTenancy;

namespace Granit.Parties.MultiTenancy;

/// <summary>
/// Granit module that wires Granit.Parties into Granit.MultiTenancy: the
/// <see cref="Handlers.SeedDefaultPartyOnTenantCreatedHandler"/> is auto-discovered by
/// Wolverine and seeds the host-scoped Party representing each new tenant at
/// provisioning time.
/// </summary>
/// <remarks>
/// Kept separate from <c>Granit.Parties</c> so apps that do not load Granit.MultiTenancy
/// do not inherit a dependency on it. Apps opt in by adding this module to their bundle.
/// </remarks>
[DependsOn(
    typeof(GranitPartiesModule),
    typeof(GranitMultiTenancyModule))]
public sealed class GranitPartiesMultiTenancyModule : GranitModule;
