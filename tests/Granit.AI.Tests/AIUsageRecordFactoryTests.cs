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
    private readonly AIUsageContext _usageContext = new();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly FakeTimeProvider _timeProvider = new();

    private AIUsageRecordFactory CreateFactory() =>
        new(_currentTenant, _currentUserService, _usageContext, _guidGenerator, _timeProvider);

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

    [Fact]
    public void Create_WithPopulatedUsageContext_StampsEnrichmentFields()
    {
        var conversationId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(false);
        _guidGenerator.Create().Returns(Guid.NewGuid());

        _usageContext.ConversationId = conversationId;
        _usageContext.PromptVersion = "1.2.0";
        _usageContext.PromptTemplateName = "summarize";
        _usageContext.PromptTemplateVersion = 3;

        AIUsageRecord record = CreateFactory().Create(
            "ws", "OpenAI", "gpt-4o", 10, 20, null);

        record.ConversationId.ShouldBe(conversationId);
        record.PromptVersion.ShouldBe("1.2.0");
        record.PromptTemplateName.ShouldBe("summarize");
        record.PromptTemplateVersion.ShouldBe(3);
    }

    [Fact]
    public void Create_WithEmptyUsageContext_LeavesEnrichmentNull()
    {
        _currentTenant.IsAvailable.Returns(false);
        _guidGenerator.Create().Returns(Guid.NewGuid());

        AIUsageRecord record = CreateFactory().Create(
            "ws", "OpenAI", "gpt-4o", 10, 20, null);

        record.ConversationId.ShouldBeNull();
        record.PromptVersion.ShouldBeNull();
        record.PromptTemplateName.ShouldBeNull();
        record.PromptTemplateVersion.ShouldBeNull();
    }

    [Fact]
    public void Clear_ResetsEveryEnrichmentField()
    {
        _usageContext.ConversationId = Guid.NewGuid();
        _usageContext.PromptVersion = "1.0.0";
        _usageContext.PromptTemplateName = "t";
        _usageContext.PromptTemplateVersion = 1;

        _usageContext.Clear();

        _usageContext.ConversationId.ShouldBeNull();
        _usageContext.PromptVersion.ShouldBeNull();
        _usageContext.PromptTemplateName.ShouldBeNull();
        _usageContext.PromptTemplateVersion.ShouldBeNull();
    }
}
