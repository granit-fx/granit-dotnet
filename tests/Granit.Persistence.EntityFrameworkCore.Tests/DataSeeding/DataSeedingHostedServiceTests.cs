// =============================================================================
// Tests — DataSeedingHostedService
// =============================================================================
// Vérifie que le hosted service :
//   - Appelle IDataSeeder.SeedAsync au démarrage avec un contexte host-level
//   - Ne propage pas les exceptions (ne bloque pas le démarrage)
//   - StopAsync complète sans effet de bord
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Tests.DataSeeding;

public sealed class DataSeedingHostedServiceTests
{
    private readonly IDataSeeder _seeder = Substitute.For<IDataSeeder>();

    [Fact]
    public async Task StartAsync_CallsSeederWithHostLevelContext()
    {
        // Arrange
        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);

        // Act
        await service.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await _seeder.Received(1).SeedAsync(
            Arg.Is<DataSeedContext>(c => c.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartAsync_SeederThrows_DoesNotPropagateException()
    {
        // Arrange
        _seeder
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Seeding failed"));

        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);

        // Act
        Func<Task> act = () => service.StartAsync(TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutSideEffects()
    {
        // Arrange
        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);

        // Act
        Func<Task> act = () => service.StopAsync(TestContext.Current.CancellationToken);

        // Assert
        await Should.NotThrowAsync(act);
        await _seeder.DidNotReceive().SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }
}
