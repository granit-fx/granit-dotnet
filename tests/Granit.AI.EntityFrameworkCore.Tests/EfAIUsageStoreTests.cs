using System.Diagnostics.Metrics;
using Granit.AI.Diagnostics;
using Granit.AI.EntityFrameworkCore.Entities;
using Granit.AI.EntityFrameworkCore.Internal;
using Granit.MultiTenancy;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;

namespace Granit.AI.EntityFrameworkCore.Tests;

public sealed class EfAIUsageStoreTests : IAsyncDisposable
{
    private readonly TestDbContextFactory _factory;
    private readonly EfAIUsageStore _store;
    private readonly ServiceProvider _sp;

    public EfAIUsageStoreTests()
    {
        DbContextOptions<AIDbContext> options = new DbContextOptionsBuilder<AIDbContext>()
            .UseInMemoryDatabase($"ai-usage-test-{Guid.NewGuid()}")
            .Options;

        ServiceCollection services = new();
        services.AddMetrics();
        _sp = services.BuildServiceProvider();
        AIMetrics metrics = new(_sp.GetRequiredService<IMeterFactory>());

        _factory = new TestDbContextFactory(options);
        _store = new EfAIUsageStore(_factory, Substitute.For<ICurrentTenant>(), metrics);
    }

    public async ValueTask DisposeAsync()
    {
        await using AIDbContext context = await _factory.CreateDbContextAsync();
        await context.Database.EnsureDeletedAsync();
        await _sp.DisposeAsync().ConfigureAwait(false);
    }

    [Fact]
    public async Task RecordAsync_PersistsUsageRecord()
    {
        AIUsageRecord record = new()
        {
            Id = Guid.NewGuid(),
            WorkspaceName = "support-chat",
            Provider = "OpenAI",
            Model = "gpt-4o",
            InputTokens = 150,
            OutputTokens = 200,
            EstimatedCostUsd = 0.00525m,
            Timestamp = DateTimeOffset.UtcNow,
            Duration = TimeSpan.FromMilliseconds(450),
        };

        await _store.RecordAsync(record, TestContext.Current.CancellationToken);

        await using AIDbContext context = await _factory.CreateDbContextAsync(TestContext.Current.CancellationToken);
        AIUsageRecordEntity? entity = await context.UsageRecords
            .FirstOrDefaultAsync(r => r.Id == record.Id, TestContext.Current.CancellationToken);

        entity.ShouldNotBeNull();
        entity.WorkspaceName.ShouldBe("support-chat");
        entity.Provider.ShouldBe("OpenAI");
        entity.Model.ShouldBe("gpt-4o");
        entity.InputTokens.ShouldBe(150);
        entity.OutputTokens.ShouldBe(200);
        entity.EstimatedCostUsd.ShouldBe(0.00525m);
    }

    private sealed class TestDbContextFactory(DbContextOptions<AIDbContext> options) : IDbContextFactory<AIDbContext>
    {
        public AIDbContext CreateDbContext() => new(options);

        public Task<AIDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new AIDbContext(options));
    }
}
