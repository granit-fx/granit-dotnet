using Granit.Modularity;
using Granit.Persistence.EntityFrameworkCore.Migrations;
using Granit.Persistence.EntityFrameworkCore.Migrations.Messages;
using Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine.Internal;
using Granit.Wolverine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Wolverine.Tests;

public sealed class GranitPersistenceEntityFrameworkCoreMigrationsWolverineModuleTests
{
    [Fact]
    public void GranitPersistenceEntityFrameworkCoreMigrationsWolverineModule_IsGranitModule() =>
        typeof(GranitPersistenceEntityFrameworkCoreMigrationsWolverineModule).IsAssignableTo(typeof(GranitModule)).ShouldBeTrue();

    [Fact]
    public void GranitPersistenceEntityFrameworkCoreMigrationsWolverineModule_IsSealed() =>
        typeof(GranitPersistenceEntityFrameworkCoreMigrationsWolverineModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void DependsOn_DeclaresmigrationAndWolverineModules()
    {
        DependsOnAttribute? attribute = typeof(GranitPersistenceEntityFrameworkCoreMigrationsWolverineModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), true)
            .OfType<DependsOnAttribute>()
            .SingleOrDefault();

        attribute.ShouldNotBeNull();
        attribute!.DependedTypes.ShouldContain(typeof(GranitPersistenceEntityFrameworkCoreMigrationsModule));
        attribute.DependedTypes.ShouldContain(typeof(GranitWolverineModule));
    }

    [Fact]
    public void ConfigureServices_ReplacesMigrationBatchDispatcher()
    {
        HostApplicationBuilder builder = Host.CreateEmptyApplicationBuilder(null);
        builder.Services.AddSingleton<IMigrationBatchDispatcher, StubMigrationBatchDispatcher>();

        ServiceConfigurationContext context = new(builder.Services, builder.Configuration, builder);
        new GranitPersistenceEntityFrameworkCoreMigrationsWolverineModule().ConfigureServices(context);

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
