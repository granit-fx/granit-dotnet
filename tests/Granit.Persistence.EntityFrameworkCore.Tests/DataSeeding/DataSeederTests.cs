// =============================================================================
// Tests — DataSeeder
// =============================================================================
// Vérifie que l'orchestrateur de seeding :
//   - Exécute tous les contributeurs enregistrés séquentiellement
//   - Continue l'exécution si un contributeur échoue (résilience)
//   - Transmet correctement le DataSeedContext à chaque contributeur
//   - Fonctionne sans contributeurs enregistrés (no-op)
//   - Propage OperationCanceledException sans l'attraper
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.DataSeeding;

public sealed class DataSeederTests
{
    private readonly ILogger<DataSeeder> _logger = NullLogger<DataSeeder>.Instance;

    [Fact]
    public async Task SeedAsync_NoContributors_CompletesWithoutError()
    {
        // Arrange
        ServiceCollection services = new();
        using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        Func<Task> act = () => seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task SeedAsync_SingleContributor_CallsSeedAsync()
    {
        // Arrange
        IDataSeedContributor contributor = Substitute.For<IDataSeedContributor>();
        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert
        await contributor.Received(1).SeedAsync(context, TestContext.Current.CancellationToken);
    }

    [Fact]
    public async Task SeedAsync_MultipleContributors_ExecutesAll()
    {
        // Arrange
        IDataSeedContributor contributor1 = Substitute.For<IDataSeedContributor>();
        IDataSeedContributor contributor2 = Substitute.For<IDataSeedContributor>();
        IDataSeedContributor contributor3 = Substitute.For<IDataSeedContributor>();
        ServiceCollection services = new();
        services.AddTransient(_ => contributor1);
        services.AddTransient(_ => contributor2);
        services.AddTransient(_ => contributor3);
        using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert
        await contributor1.Received(1).SeedAsync(context, Arg.Any<CancellationToken>());
        await contributor2.Received(1).SeedAsync(context, Arg.Any<CancellationToken>());
        await contributor3.Received(1).SeedAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_ContributorThrows_ContinuesWithRemaining()
    {
        // Arrange
        IDataSeedContributor failingContributor = Substitute.For<IDataSeedContributor>();
        failingContributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Seed failed"));

        IDataSeedContributor successContributor = Substitute.For<IDataSeedContributor>();

        ServiceCollection services = new();
        services.AddTransient(_ => failingContributor);
        services.AddTransient(_ => successContributor);
        using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert — second contributor was still called
        await successContributor.Received(1).SeedAsync(context, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SeedAsync_PassesContextToContributor()
    {
        // Arrange
        var tenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
        DataSeedContext context = new(tenantId);
        context["AdminEmail"] = "admin@test.com";

        DataSeedContext? capturedContext = null;
        IDataSeedContributor contributor = Substitute.For<IDataSeedContributor>();
        contributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedContext = callInfo.Arg<DataSeedContext>();
                return Task.CompletedTask;
            });

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);

        // Act
        await seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert
        capturedContext.ShouldNotBeNull();
        capturedContext!.TenantId.ShouldBe(tenantId);
        capturedContext["AdminEmail"].ShouldBe("admin@test.com");
    }

    [Fact]
    public async Task SeedAsync_OperationCanceledException_Propagates()
    {
        // Arrange
        IDataSeedContributor contributor = Substitute.For<IDataSeedContributor>();
        contributor
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        ServiceCollection services = new();
        services.AddTransient(_ => contributor);
        using ServiceProvider sp = services.BuildServiceProvider();
        DataSeeder seeder = new(sp.GetRequiredService<IServiceScopeFactory>(), _logger);
        DataSeedContext context = new();

        // Act
        Func<Task> act = () => seeder.SeedAsync(context, TestContext.Current.CancellationToken);

        // Assert — OperationCanceledException is NOT caught
        await Should.ThrowAsync<OperationCanceledException>(act);
    }
}
