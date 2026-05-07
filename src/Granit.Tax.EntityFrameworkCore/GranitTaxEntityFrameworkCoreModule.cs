using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;

namespace Granit.Tax.EntityFrameworkCore;

/// <summary>EF Core persistence for Granit.Tax.</summary>
[DependsOn(
    typeof(GranitEncryptionEntityFrameworkCoreModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitTaxModule))]
public sealed class GranitTaxEntityFrameworkCoreModule : GranitModule;
