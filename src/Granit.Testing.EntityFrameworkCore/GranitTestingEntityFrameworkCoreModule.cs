using Granit.Core.Modularity;
using Granit.Persistence;
using Granit.Testing;

namespace Granit.Testing.EntityFrameworkCore;

/// <summary>
/// Granit module for EF Core test infrastructure.
/// </summary>
/// <remarks>
/// Provides <see cref="InMemoryDbContextFactory{TContext}"/> and
/// <see cref="SqliteDbContextFactory{TContext}"/> with automatic Granit
/// interceptor wiring (audit, versioning, soft-delete).
/// This is a utility module with no runtime service registration.
/// </remarks>
[DependsOn(
    typeof(GranitPersistenceModule),
    typeof(GranitTestingModule))]
public sealed class GranitTestingEntityFrameworkCoreModule : GranitModule;
