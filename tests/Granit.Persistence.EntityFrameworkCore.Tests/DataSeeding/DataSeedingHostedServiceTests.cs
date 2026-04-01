// =============================================================================
// Tests — DataSeedingHostedService
// =============================================================================
// Vérifie que le hosted service :
//   - Appelle IDataSeeder.SeedAsync dans StartedAsync (pas StartAsync) pour
//     garantir que tous les IHostedService ont démarré (y compris Wolverine)
//   - Ne propage pas les exceptions (ne bloque pas le démarrage)
//   - StartAsync/StopAsync complètent sans effet de bord
// =============================================================================

using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Microsoft.Extensions.Hosting;
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
    public void ImplementsIHostedLifecycleService() =>
        typeof(DataSeedingHostedService).IsAssignableTo(typeof(IHostedLifecycleService)).ShouldBeTrue();

    [Fact]
    public async Task StartedAsync_CallsSeederWithHostLevelContext()
    {
        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);
        IHostedLifecycleService lifecycle = service;

        await lifecycle.StartedAsync(TestContext.Current.CancellationToken);

        await _seeder.Received(1).SeedAsync(
            Arg.Is<DataSeedContext>(c => c.TenantId == null),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StartedAsync_SeederThrows_DoesNotPropagateException()
    {
        _seeder
            .SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Seeding failed"));

        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);
        IHostedLifecycleService lifecycle = service;

        Func<Task> act = () => lifecycle.StartedAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
    }

    [Fact]
    public async Task StartAsync_DoesNotCallSeeder()
    {
        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);
        IHostedService hostedService = service;

        await hostedService.StartAsync(TestContext.Current.CancellationToken);

        await _seeder.DidNotReceive().SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task StopAsync_CompletesWithoutSideEffects()
    {
        DataSeedingHostedService service = new(_seeder, NullLogger<DataSeedingHostedService>.Instance);
        IHostedService hostedService = service;

        Func<Task> act = () => hostedService.StopAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(act);
        await _seeder.DidNotReceive().SeedAsync(Arg.Any<DataSeedContext>(), Arg.Any<CancellationToken>());
    }
}
