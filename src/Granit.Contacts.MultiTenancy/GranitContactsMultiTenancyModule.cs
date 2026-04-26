using Granit.Modularity;
using Granit.MultiTenancy;

namespace Granit.Contacts.MultiTenancy;

/// <summary>
/// Granit module that wires Granit.Contacts into Granit.MultiTenancy: the
/// <see cref="Handlers.SeedDefaultContactOnTenantCreatedHandler"/> is auto-discovered by
/// Wolverine and seeds the host-scoped Contact representing each new tenant at
/// provisioning time.
/// </summary>
/// <remarks>
/// Kept separate from <c>Granit.Contacts</c> so apps that do not load Granit.MultiTenancy
/// do not inherit a dependency on it. Apps opt in by adding this module to their bundle.
/// </remarks>
[DependsOn(
    typeof(GranitContactsModule),
    typeof(GranitMultiTenancyModule))]
public sealed class GranitContactsMultiTenancyModule : GranitModule;
