using Granit.Core.Modularity;
using Granit.Persistence.Migrations;
using Granit.Persistence.Migrations.Messages;
using Granit.Persistence.Migrations.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.Migrations.Wolverine.Tests;

public sealed class GranitPersistenceMigrationsWolverineModuleTests
{
    [Fact]
    public void GranitPersistenceMigrationsWolverineModule_IsGranitModule() =>
        typeof(GranitPersistenceMigrationsWolverineModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitPersistenceMigrationsWolverineModule_IsSealed() =>
        typeof(GranitPersistenceMigrationsWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void DependsOn_DeclaresmigrationAndWolverineModules()
    {
        DependsOnAttribute? attribute = typeof(GranitPersistenceMigrationsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitPersistenceMigrationsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_ReplacesMigrationBatchDispatcher()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<IMigrationBatchDispatcher, StubMigrationBatchDispatcher>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitPersistenceMigrationsWolverineModule().ConfigureServices(context);

        ServiceDescriptor? descriptor = builder.Services.FirstOrDefault(
            d => d.ServiceType == typeof(IMigrationBatchDispatcher));
        descriptor.ShouldNotBeNull();
        descriptor!.ImplementationType.ShouldBe(typeof(WolverineMigrationBatchDispatcher));
    }

    private sealed class StubMigrationBatchDispatcher : IMigrationBatchDispatcher
    {
        public Task DispatchAsync(RunMigrationBatchCommand command, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DispatchAsync(IEnumerable<RunMigrationBatchCommand> commands, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
