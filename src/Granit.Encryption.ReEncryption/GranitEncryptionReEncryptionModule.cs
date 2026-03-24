using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;

namespace Granit.Encryption.ReEncryption;

/// <summary>Module descriptor for Granit.Encryption.ReEncryption.</summary>
[DependsOn(typeof(GranitEncryptionEntityFrameworkCoreModule))]
public sealed class GranitEncryptionReEncryptionModule : GranitModule;
