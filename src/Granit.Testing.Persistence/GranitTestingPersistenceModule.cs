using Granit.Guids;
using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore;
using Granit.Persistence.EntityFrameworkCore.Migrations;

namespace Granit.Testing.Persistence;

/// <summary>
/// Granit module for the persistence provider conformance kit: abstract xUnit suites that
/// provider integration projects inherit and run against a real database (Testcontainers).
/// </summary>
[DependsOn(
    typeof(GranitGuidsModule),
    typeof(GranitPersistenceEntityFrameworkCoreMigrationsModule),
    typeof(GranitPersistenceEntityFrameworkCoreModule),
    typeof(GranitTestingModule))]
public sealed class GranitTestingPersistenceModule : GranitModule;
