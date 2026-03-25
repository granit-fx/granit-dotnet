using Granit.Encryption.EntityFrameworkCore;
using Granit.Modularity;

namespace Granit.Encryption.BackgroundJobs;

/// <summary>Module descriptor for Granit.Encryption.BackgroundJobs.</summary>
[DependsOn(typeof(GranitEncryptionEntityFrameworkCoreModule))]
public sealed class GranitEncryptionBackgroundJobsModule : GranitModule;
