using Granit.Persistence.EntityFrameworkCore.DataSeeding;
using Granit.ReferenceData.EntityFrameworkCore.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.EntityFrameworkCore.Tests;

public sealed class ReferenceDataSeedContributorTests
{
    [Fact]
    public async Task SeedAsync_ExecutesSeedersInOrder()
    {
        // Arrange
        List<int> executionOrder = [];

        IReferenceDataSeeder<TestEntity> seeder1 = CreateSeeder(1, () => executionOrder.Add(1));
        IReferenceDataSeeder<TestEntity> seeder2 = CreateSeeder(2, () => executionOrder.Add(2));
        IReferenceDataSeeder<TestEntity> seeder3 = CreateSeeder(3, () => executionOrder.Add(3));

        // Register in reverse order to verify sorting
        ReferenceDataSeedContributor<TestEntity> contributor = BuildContributor(
            [seeder3, seeder1, seeder2]);

        // Act
        await contributor.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert
        executionOrder.ShouldBe([1, 2, 3]);
    }

    [Fact]
    public async Task SeedAsync_ContinuesAfterSeederFailure()
    {
        // Arrange
        List<int> executionOrder = [];

        IReferenceDataSeeder<TestEntity> seeder1 = CreateSeeder(1, () => executionOrder.Add(1));
        IReferenceDataSeeder<TestEntity> failingSeeder = Substitute.For<IReferenceDataSeeder<TestEntity>>();
        failingSeeder.Order.Returns(2);
        failingSeeder.SeedAsync(Arg.Any<IReferenceDataStoreReader<TestEntity>>(), Arg.Any<IReferenceDataStoreWriter<TestEntity>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new InvalidOperationException("Seed failed"));
        IReferenceDataSeeder<TestEntity> seeder3 = CreateSeeder(3, () => executionOrder.Add(3));

        ReferenceDataSeedContributor<TestEntity> contributor = BuildContributor(
            [seeder1, failingSeeder, seeder3]);

        // Act — should not throw
        await contributor.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken);

        // Assert — seeder 1 and 3 ran; failing seeder did not prevent 3
        executionOrder.ShouldBe([1, 3]);
    }

    [Fact]
    public async Task SeedAsync_PropagatesOperationCanceledException()
    {
        // Arrange
        IReferenceDataSeeder<TestEntity> seeder = Substitute.For<IReferenceDataSeeder<TestEntity>>();
        seeder.Order.Returns(1);
        seeder.SeedAsync(Arg.Any<IReferenceDataStoreReader<TestEntity>>(), Arg.Any<IReferenceDataStoreWriter<TestEntity>>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new OperationCanceledException());

        ReferenceDataSeedContributor<TestEntity> contributor = BuildContributor([seeder]);

        // Act & Assert
        await Should.ThrowAsync<OperationCanceledException>(
            () => contributor.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SeedAsync_NoSeeders_DoesNotThrow()
    {
        // Arrange
        ReferenceDataSeedContributor<TestEntity> contributor = BuildContributor([]);

        // Act & Assert
        await Should.NotThrowAsync(
            () => contributor.SeedAsync(new DataSeedContext(), TestContext.Current.CancellationToken));
    }

    // ── Helpers ─────────────────────────────────────────────────────────────

    private static IReferenceDataSeeder<TestEntity> CreateSeeder(int order, Action onSeed)
    {
        IReferenceDataSeeder<TestEntity> seeder = Substitute.For<IReferenceDataSeeder<TestEntity>>();
        seeder.Order.Returns(order);
        seeder.SeedAsync(Arg.Any<IReferenceDataStoreReader<TestEntity>>(), Arg.Any<IReferenceDataStoreWriter<TestEntity>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                onSeed();
                return Task.CompletedTask;
            });
        return seeder;
    }

    private static ReferenceDataSeedContributor<TestEntity> BuildContributor(
        IEnumerable<IReferenceDataSeeder<TestEntity>> seeders)
    {
        ServiceCollection services = new();
        IReferenceDataStoreReader<TestEntity> storeReader = Substitute.For<IReferenceDataStoreReader<TestEntity>>();
        IReferenceDataStoreWriter<TestEntity> storeWriter = Substitute.For<IReferenceDataStoreWriter<TestEntity>>();
        services.AddSingleton(storeReader);
        services.AddSingleton(storeWriter);
        foreach (IReferenceDataSeeder<TestEntity> seeder in seeders)
        {
            services.AddSingleton(seeder);
        }
        services.AddLogging();

        ServiceProvider sp = services.BuildServiceProvider();
        ILogger<ReferenceDataSeedContributor<TestEntity>> logger =
            sp.GetRequiredService<ILogger<ReferenceDataSeedContributor<TestEntity>>>();

        return new ReferenceDataSeedContributor<TestEntity>(sp, logger);
    }
}
