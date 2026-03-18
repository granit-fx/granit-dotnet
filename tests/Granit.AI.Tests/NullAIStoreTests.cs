using Granit.AI.Internal;
using Granit.AI.Workspaces;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class NullAIWorkspaceStoreReaderTests
{
    private readonly NullAIWorkspaceStoreReader _sut = new();

    [Fact]
    public async Task FindAsync_AnyName_ReturnsNull()
    {
        AIWorkspace? result = await _sut.FindAsync("any-workspace", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsEmptyList()
    {
        IReadOnlyList<AIWorkspace> result = await _sut.GetAllAsync(TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }
}

public sealed class NullAIWorkspaceStoreWriterTests
{
    private readonly NullAIWorkspaceStoreWriter _sut = new();

    private static AIWorkspace CreateWorkspace() =>
        new() { Name = "test", Provider = "OpenAI", Model = "gpt-4o" };

    [Fact]
    public async Task SaveAsync_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => _sut.SaveAsync(CreateWorkspace(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UpdateAsync_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => _sut.UpdateAsync(CreateWorkspace(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_DoesNotThrow()
    {
        await Should.NotThrowAsync(() => _sut.DeleteAsync("any-workspace", TestContext.Current.CancellationToken));
    }
}

public sealed class NullAIUsageTrackerTests
{
    private readonly NullAIUsageTracker _sut = new();

    [Fact]
    public async Task RecordAsync_DoesNotThrow()
    {
        AIUsageRecord record = new()
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTimeOffset.UtcNow,
            WorkspaceName = "test",
            Provider = "OpenAI",
            Model = "gpt-4o",
            InputTokens = 100,
            OutputTokens = 50,
        };

        await Should.NotThrowAsync(() => _sut.RecordAsync(record, TestContext.Current.CancellationToken));
    }
}
