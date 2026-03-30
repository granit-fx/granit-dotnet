using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class MigrationProgressDbEnsurerTests
{
    private static IDbContextFactory<MigrationProgressDbContext> CreateFactory()
    {
        string dbName = Guid.NewGuid().ToString();
        ServiceCollection services = new();
        services.AddDbContextFactory<MigrationProgressDbContext>(
            opts => opts.UseInMemoryDatabase(dbName));
        ServiceProvider sp = services.BuildServiceProvider();
        return sp.GetRequiredService<IDbContextFactory<MigrationProgressDbContext>>();
    }

    [Fact]
    public async Task EnsureCreatedAsync_WithInMemoryProvider_DoesNotThrow()
    {
        MigrationProgressDbEnsurer sut = new(CreateFactory());

        await Should.NotThrowAsync(() =>
            sut.EnsureCreatedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureCreatedAsync_CalledTwice_DoesNotThrow()
    {
        MigrationProgressDbEnsurer sut = new(CreateFactory());

        await sut.EnsureCreatedAsync(TestContext.Current.CancellationToken);

        await Should.NotThrowAsync(() =>
            sut.EnsureCreatedAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task EnsureCreatedAsync_FactoryThrows_PropagatesException()
    {
        IDbContextFactory<MigrationProgressDbContext> factory =
            Substitute.For<IDbContextFactory<MigrationProgressDbContext>>();
        factory.CreateDbContextAsync(Arg.Any<CancellationToken>())
            .Returns<MigrationProgressDbContext>(_ =>
                throw new InvalidOperationException("db unavailable"));

        MigrationProgressDbEnsurer sut = new(factory);

        await Should.ThrowAsync<InvalidOperationException>(() =>
            sut.EnsureCreatedAsync(TestContext.Current.CancellationToken));
    }
}
