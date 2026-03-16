using Granit.Core.Modularity;
using Granit.Persistence;

namespace Granit.Encryption.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core field-level encryption.
/// Provides <see cref="EncryptedAttribute"/> and <see cref="EncryptedStringConverter"/>
/// backed by <see cref="IStringEncryptionService"/>.
/// </summary>
[DependsOn(
    typeof(GranitEncryptionModule),
    typeof(GranitPersistenceModule))]
public sealed class GranitEncryptionEntityFrameworkCoreModule : GranitModule;
