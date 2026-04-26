using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Contacts.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Contacts.</summary>
[DependsOn(
    typeof(GranitContactsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule))]
public sealed class GranitContactsEntityFrameworkCoreModule : GranitModule;
