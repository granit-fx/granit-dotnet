using Granit.AI.Internal;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Users;
using Microsoft.Extensions.Time.Testing;
using NSubstitute;
using Shouldly;

namespace Granit.AI.Tests;

public sealed class AIUsageRecordFactoryTests
{
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly ICurrentUserService _currentUserService = Substitute.For<ICurrentUserService>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly FakeTimeProvider _timeProvider = new();

    private AIUsageRecordFactory CreateFactory() =>
        new(_currentTenant, _currentUserService, _guidGenerator, _timeProvider);

    [Fact]
    public void Create_WithTenantAndUser_PopulatesBothIds()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var recordId = Guid.NewGuid();

        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);
        _currentUserService.UserId.Returns(userId.ToString());
        _guidGenerator.Create().Returns(recordId);

        AIUsageRecord record = CreateFactory().Create(
            "workspace-1", "OpenAI", "gpt-4o", 100, 50, TimeSpan.FromMilliseconds(200));

        record.Id.ShouldBe(recordId);
        record.TenantId.ShouldBe(tenantId);
        record.UserId.ShouldBe(userId);
        record.WorkspaceName.ShouldBe("workspace-1");
        record.Provider.ShouldBe("OpenAI");
        record.Model.ShouldBe("gpt-4o");
        record.InputTokens.ShouldBe(100);
        record.OutputTokens.ShouldBe(50);
        record.Duration.ShouldBe(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void Create_WithoutTenant_LeavesNull()
    {
        _currentTenant.IsAvailable.Returns(false);
        _currentUserService.UserId.Returns((string?)null);
        _guidGenerator.Create().Returns(Guid.NewGuid());

        AIUsageRecord record = CreateFactory().Create(
            "ws", "Anthropic", "claude-sonnet-4-6", 10, 20, null);

        record.TenantId.ShouldBeNull();
        record.UserId.ShouldBeNull();
        record.Duration.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNonGuidUserId_LeavesNull()
    {
        _currentTenant.IsAvailable.Returns(false);
        _currentUserService.UserId.Returns("not-a-guid");
        _guidGenerator.Create().Returns(Guid.NewGuid());

        AIUsageRecord record = CreateFactory().Create(
            "ws", "OpenAI", "gpt-4o", 10, 20, null);

        record.UserId.ShouldBeNull();
    }
}
