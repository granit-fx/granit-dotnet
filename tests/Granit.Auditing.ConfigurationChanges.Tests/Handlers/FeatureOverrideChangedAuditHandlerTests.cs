using Granit.Auditing.ConfigurationChanges.Handlers;
using Granit.Auditing.Domain;
using Granit.Features.Events;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Users;
using NSubstitute;
using Xunit;

namespace Granit.Auditing.ConfigurationChanges.Tests.Handlers;

public sealed class FeatureOverrideChangedAuditHandlerTests
{
    private readonly IAuditingWriter _writer = Substitute.For<IAuditingWriter>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    public FeatureOverrideChangedAuditHandlerTests()
    {
        _currentUser.UserId.Returns("test-user");
        _currentUser.UserName.Returns("Test User");
        _currentTenant.IsAvailable.Returns(false);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

    [Fact]
    public async Task HandleAsync_PersistsAuditEntry_WithConfigurationChangeCategory()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, null, "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.Category == AuditCategory.ConfigurationChange),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsEntityTypeToFeatureOverride()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("DarkMode", null, "false", "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().EntityType == "FeatureOverride" &&
                e.EntityChanges.First().EntityId == "DarkMode"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NewOverride_ChangeTypeIsCreated()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, null, "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Created),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RemovedOverride_ChangeTypeIsDeleted()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, "true", null, DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Deleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ModifiedOverride_ChangeTypeIsModified()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, "false", "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Modified),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTenantIdOnEvent_UsesTenantIdFromEvent()
    {
        var tenantId = Guid.NewGuid();
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", tenantId, "false", "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNoTenantIdOnEvent_FallsBackToCurrentTenant()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, "false", "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsPropertyChangeValues()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("DarkMode", null, "false", "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().PropertyChanges.First().PropertyName == "Value" &&
                e.EntityChanges.First().PropertyChanges.First().OriginalValue == "false" &&
                e.EntityChanges.First().PropertyChanges.First().NewValue == "true"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNullUserId_FallsBackToSystem()
    {
        _currentUser.UserId.Returns((string?)null);

        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, null, "true", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.UserId == "system"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsForeignKeysCorrectly()
    {
        FeatureOverrideChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        FeatureOverrideChangedEvent evt = new("FeatureX", null, "old", "new", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().AuditEntryId == e.Id &&
                e.EntityChanges.First().PropertyChanges.First().AuditEntityChangeId == e.EntityChanges.First().Id),
            Arg.Any<CancellationToken>());
    }
}
