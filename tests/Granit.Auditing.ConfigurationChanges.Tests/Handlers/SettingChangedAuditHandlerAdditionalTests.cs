using Granit.Auditing.Abstractions;
using Granit.Auditing.ConfigurationChanges.Handlers;
using Granit.Auditing.Domain;
using Granit.Guids;
using Granit.MultiTenancy;
using Granit.Settings.Events;
using Granit.Users;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Auditing.ConfigurationChanges.Tests.Handlers;

public sealed class SettingChangedAuditHandlerAdditionalTests
{
    private readonly IAuditingWriter _writer = Substitute.For<IAuditingWriter>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly ICurrentTenant _currentTenant = Substitute.For<ICurrentTenant>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();

    public SettingChangedAuditHandlerAdditionalTests()
    {
        _currentUser.UserId.Returns("test-user");
        _currentUser.UserName.Returns("Test User");
        _currentTenant.IsAvailable.Returns(false);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());
    }

    [Fact]
    public async Task HandleAsync_NewSetting_ChangeTypeIsCreated()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, null, "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Created),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_DeletedSetting_ChangeTypeIsDeleted()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "dark", null, DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Deleted),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ModifiedSetting_ChangeTypeIsModified()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().ChangeType == AuditChangeType.Modified),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsEntityId_WithSettingNameProviderAndKey()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", "user-42", "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().EntityId == "App.Theme:G:user-42"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_NullProviderKey_UsesGlobal()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().EntityId == "App.Theme:G:global"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithTenant_SetsTenantId()
    {
        var tenantId = Guid.NewGuid();
        _currentTenant.IsAvailable.Returns(true);
        _currentTenant.Id.Returns(tenantId);

        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNullUserId_FallsBackToSystem()
    {
        _currentUser.UserId.Returns((string?)null);

        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.UserId == "system"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsTimestamp_FromEvent()
    {
        DateTimeOffset timestamp = new(2026, 1, 15, 10, 30, 0, TimeSpan.Zero);

        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", timestamp);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e => e.Timestamp == timestamp),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsPropertyChangeValues()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "light", "dark", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().PropertyChanges.First().PropertyName == "Value" &&
                e.EntityChanges.First().PropertyChanges.First().OriginalValue == "light" &&
                e.EntityChanges.First().PropertyChanges.First().NewValue == "dark"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_SetsForeignKeysCorrectly()
    {
        SettingChangedAuditHandler handler = new(_writer, _currentUser, _currentTenant, _guidGenerator);
        SettingChangedEvent evt = new("App.Theme", "G", null, "old", "new", DateTimeOffset.UtcNow);

        await handler.HandleAsync(evt, TestContext.Current.CancellationToken);

        await _writer.Received(1).WriteAsync(
            Arg.Is<AuditEntry>(e =>
                e.EntityChanges.First().AuditEntryId == e.Id &&
                e.EntityChanges.First().PropertyChanges.First().AuditEntityChangeId == e.EntityChanges.First().Id),
            Arg.Any<CancellationToken>());
    }
}
