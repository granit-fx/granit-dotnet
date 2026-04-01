using System.Diagnostics.Metrics;
using Granit.Events;
using Granit.Identity.Local.Diagnostics;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Events;
using Granit.Identity.Local.Services;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using OpenIddict.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

public sealed class AspNetImpersonationServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly UserManager<GranitUser> _userManager;
    private readonly IOpenIddictTokenManager _tokenManager = Substitute.For<IOpenIddictTokenManager>();
    private readonly IDistributedEventBus _eventBus = Substitute.For<IDistributedEventBus>();
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly AspNetImpersonationService _sut;

    public AspNetImpersonationServiceTests()
    {
        IUserStore<GranitUser> store = Substitute.For<IUserStore<GranitUser>>();
        _userManager = Substitute.For<UserManager<GranitUser>>(
            store, null, null, null, null, null, null, null, null);

        _clock.Now.Returns(FixedNow);

        IMeterFactory meterFactory = new TestMeterFactory();
        IdentityLocalMetrics metrics = new(meterFactory);

        _sut = new AspNetImpersonationService(
            _userManager,
            _tokenManager,
            _eventBus,
            metrics,
            _clock,
            NullLogger<AspNetImpersonationService>.Instance);
    }

    // ────────────────────── ImpersonateAsync ──────────────────────

    [Fact]
    public async Task ImpersonateAsync_ValidInputs_ReturnsTokenResult()
    {
        GranitUser target = CreateUser();
        string targetId = target.Id.ToString();
        var impersonatorGuid = Guid.NewGuid();
        string impersonatorId = impersonatorGuid.ToString();

        _userManager.FindByIdAsync(targetId).Returns(target);
        _userManager.GetRolesAsync(target).Returns(["Admin", "User"]);

        object accessTokenEntry = new object();
        object refreshTokenEntry = new object();
        _tokenManager.CreateAsync(Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.AccessToken), Arg.Any<CancellationToken>())
            .Returns(accessTokenEntry);
        _tokenManager.CreateAsync(Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.RefreshToken), Arg.Any<CancellationToken>())
            .Returns(refreshTokenEntry);
        _tokenManager.GetPayloadAsync(accessTokenEntry, Arg.Any<CancellationToken>())
            .Returns("access-token-value");
        _tokenManager.GetPayloadAsync(refreshTokenEntry, Arg.Any<CancellationToken>())
            .Returns("refresh-token-value");

        ImpersonationResult result = await _sut.ImpersonateAsync(
            targetId, impersonatorId, "Admin User", TestContext.Current.CancellationToken);

        result.AccessToken.ShouldBe("access-token-value");
        result.RefreshToken.ShouldBe("refresh-token-value");
        result.ExpiresIn.ShouldBe(3600);
    }

    [Fact]
    public async Task ImpersonateAsync_PublishesUserImpersonatedEto()
    {
        GranitUser target = CreateUser();
        var tenantId = Guid.NewGuid();
        target.TenantId = tenantId;
        string targetId = target.Id.ToString();
        var impersonatorGuid = Guid.NewGuid();
        string impersonatorId = impersonatorGuid.ToString();

        _userManager.FindByIdAsync(targetId).Returns(target);
        _userManager.GetRolesAsync(target).Returns([]);
        SetupTokenCreation();

        await _sut.ImpersonateAsync(
            targetId, impersonatorId, "Admin User", TestContext.Current.CancellationToken);

        await _eventBus.Received(1).PublishAsync(
            Arg.Is<UserImpersonatedEto>(e =>
                e.TargetUserId == target.Id &&
                e.ImpersonatorId == impersonatorGuid &&
                e.TenantId == tenantId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImpersonateAsync_CreatesAccessAndRefreshTokens()
    {
        GranitUser target = CreateUser();
        string targetId = target.Id.ToString();
        var impersonatorGuid = Guid.NewGuid();

        _userManager.FindByIdAsync(targetId).Returns(target);
        _userManager.GetRolesAsync(target).Returns([]);
        SetupTokenCreation();

        await _sut.ImpersonateAsync(
            targetId, impersonatorGuid.ToString(), "Admin", TestContext.Current.CancellationToken);

        await _tokenManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.AccessToken &&
                d.Subject == targetId &&
                d.CreationDate == FixedNow &&
                d.ExpirationDate == FixedNow + TimeSpan.FromHours(1)),
            Arg.Any<CancellationToken>());

        await _tokenManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.RefreshToken &&
                d.Subject == targetId),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ImpersonateAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((GranitUser?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.ImpersonateAsync(
                unknownId, Guid.NewGuid().ToString(), "Admin", TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ImpersonateAsync_NullOrWhiteSpaceTargetUserId_ThrowsArgument(string? targetUserId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ImpersonateAsync(
                targetUserId!, Guid.NewGuid().ToString(), "Admin", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ImpersonateAsync_NullOrWhiteSpaceImpersonatorId_ThrowsArgument(string? impersonatorId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ImpersonateAsync(
                Guid.NewGuid().ToString(), impersonatorId!, "Admin", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task ImpersonateAsync_NullOrWhiteSpaceImpersonatorName_ThrowsArgument(string? impersonatorName)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.ImpersonateAsync(
                Guid.NewGuid().ToString(), Guid.NewGuid().ToString(), impersonatorName!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ImpersonateAsync_NullPayload_ReturnsEmptyToken()
    {
        GranitUser target = CreateUser();
        string targetId = target.Id.ToString();

        _userManager.FindByIdAsync(targetId).Returns(target);
        _userManager.GetRolesAsync(target).Returns([]);

        object tokenEntry = new object();
        _tokenManager.CreateAsync(Arg.Any<OpenIddictTokenDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(tokenEntry);
        _tokenManager.GetPayloadAsync(tokenEntry, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        ImpersonationResult result = await _sut.ImpersonateAsync(
            targetId, Guid.NewGuid().ToString(), "Admin", TestContext.Current.CancellationToken);

        result.AccessToken.ShouldBe(string.Empty);
        result.RefreshToken.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ImpersonateAsync_UserWithNoRoles_SucceedsWithoutRoleClaims()
    {
        GranitUser target = CreateUser();
        string targetId = target.Id.ToString();

        _userManager.FindByIdAsync(targetId).Returns(target);
        _userManager.GetRolesAsync(target).Returns([]);
        SetupTokenCreation();

        ImpersonationResult result = await _sut.ImpersonateAsync(
            targetId, Guid.NewGuid().ToString(), "Admin", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ────────────────────── BackToImpersonatorAsync ──────────────────────

    [Fact]
    public async Task BackToImpersonatorAsync_ValidId_ReturnsTokenResult()
    {
        GranitUser admin = CreateUser();
        string adminId = admin.Id.ToString();

        _userManager.FindByIdAsync(adminId).Returns(admin);
        _userManager.GetRolesAsync(admin).Returns(["Admin"]);

        object accessTokenEntry = new object();
        object refreshTokenEntry = new object();
        _tokenManager.CreateAsync(Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.AccessToken), Arg.Any<CancellationToken>())
            .Returns(accessTokenEntry);
        _tokenManager.CreateAsync(Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.RefreshToken), Arg.Any<CancellationToken>())
            .Returns(refreshTokenEntry);
        _tokenManager.GetPayloadAsync(accessTokenEntry, Arg.Any<CancellationToken>())
            .Returns("admin-access-token");
        _tokenManager.GetPayloadAsync(refreshTokenEntry, Arg.Any<CancellationToken>())
            .Returns("admin-refresh-token");

        ImpersonationResult result = await _sut.BackToImpersonatorAsync(
            adminId, TestContext.Current.CancellationToken);

        result.AccessToken.ShouldBe("admin-access-token");
        result.RefreshToken.ShouldBe("admin-refresh-token");
        result.ExpiresIn.ShouldBe(3600);
    }

    [Fact]
    public async Task BackToImpersonatorAsync_CreatesTokensWithCorrectExpiry()
    {
        GranitUser admin = CreateUser();
        string adminId = admin.Id.ToString();

        _userManager.FindByIdAsync(adminId).Returns(admin);
        _userManager.GetRolesAsync(admin).Returns([]);
        SetupTokenCreation();

        await _sut.BackToImpersonatorAsync(adminId, TestContext.Current.CancellationToken);

        await _tokenManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.AccessToken &&
                d.Subject == adminId &&
                d.ExpirationDate == FixedNow + TimeSpan.FromHours(1)),
            Arg.Any<CancellationToken>());

        await _tokenManager.Received(1).CreateAsync(
            Arg.Is<OpenIddictTokenDescriptor>(d =>
                d.Type == OpenIddictConstants.TokenTypeHints.RefreshToken &&
                d.Subject == adminId &&
                d.ExpirationDate == FixedNow + TimeSpan.FromDays(14)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task BackToImpersonatorAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((GranitUser?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.BackToImpersonatorAsync(unknownId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task BackToImpersonatorAsync_NullOrWhiteSpaceId_ThrowsArgument(string? impersonatorId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.BackToImpersonatorAsync(impersonatorId!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task BackToImpersonatorAsync_NullPayload_ReturnsEmptyToken()
    {
        GranitUser admin = CreateUser();
        string adminId = admin.Id.ToString();

        _userManager.FindByIdAsync(adminId).Returns(admin);
        _userManager.GetRolesAsync(admin).Returns([]);

        object tokenEntry = new object();
        _tokenManager.CreateAsync(Arg.Any<OpenIddictTokenDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(tokenEntry);
        _tokenManager.GetPayloadAsync(tokenEntry, Arg.Any<CancellationToken>())
            .Returns((string?)null);

        ImpersonationResult result = await _sut.BackToImpersonatorAsync(
            adminId, TestContext.Current.CancellationToken);

        result.AccessToken.ShouldBe(string.Empty);
        result.RefreshToken.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task BackToImpersonatorAsync_UserWithNullUserName_UsesEmptyString()
    {
        GranitUser admin = CreateUser();
        admin.UserName = null;
        string adminId = admin.Id.ToString();

        _userManager.FindByIdAsync(adminId).Returns(admin);
        _userManager.GetRolesAsync(admin).Returns([]);
        SetupTokenCreation();

        ImpersonationResult result = await _sut.BackToImpersonatorAsync(
            adminId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    [Fact]
    public async Task BackToImpersonatorAsync_UserWithNullEmail_UsesEmptyString()
    {
        GranitUser admin = CreateUser();
        admin.Email = null;
        string adminId = admin.Id.ToString();

        _userManager.FindByIdAsync(adminId).Returns(admin);
        _userManager.GetRolesAsync(admin).Returns([]);
        SetupTokenCreation();

        ImpersonationResult result = await _sut.BackToImpersonatorAsync(
            adminId, TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
    }

    // ────────────────────── Helpers ──────────────────────

    private static GranitUser CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "testuser",
        Email = "test@example.com",
    };

    private void SetupTokenCreation()
    {
        object tokenEntry = new object();
        _tokenManager.CreateAsync(Arg.Any<OpenIddictTokenDescriptor>(), Arg.Any<CancellationToken>())
            .Returns(tokenEntry);
        _tokenManager.GetPayloadAsync(tokenEntry, Arg.Any<CancellationToken>())
            .Returns("mock-token");
    }

    /// <summary>
    /// Minimal <see cref="IMeterFactory"/> for unit tests — returns a fresh <see cref="Meter"/>
    /// per call without registration.
    /// </summary>
    private sealed class TestMeterFactory : IMeterFactory
    {
        public Meter Create(MeterOptions options) => new(options);
        public void Dispose() { }
    }
}
