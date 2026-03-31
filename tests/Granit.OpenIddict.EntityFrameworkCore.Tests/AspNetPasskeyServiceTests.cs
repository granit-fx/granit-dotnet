using System.Text.Json;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Options;
using Granit.Identity.Local.Services;
using Granit.OpenIddict.EntityFrameworkCore.Internal;
using Granit.Timing;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.OpenIddict.EntityFrameworkCore.Tests;

public sealed class AspNetPasskeyServiceTests
{
    private static readonly DateTimeOffset FixedNow = new(2025, 6, 15, 10, 0, 0, TimeSpan.Zero);

    private readonly UserManager<GranitUser> _userManager;
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly GranitPasskeyOptions _passkeyOptions;
    private readonly AspNetPasskeyService _sut;

    public AspNetPasskeyServiceTests()
    {
        IUserStore<GranitUser> store = Substitute.For<IUserStore<GranitUser>>();
        _userManager = Substitute.For<UserManager<GranitUser>>(
            store, null, null, null, null, null, null, null, null);

        _clock.Now.Returns(FixedNow);

        _passkeyOptions = new GranitPasskeyOptions
        {
            ServerDomain = "example.com",
            AuthenticatorTimeout = TimeSpan.FromMinutes(5),
            ChallengeSize = 32,
        };

        _sut = new AspNetPasskeyService(
            _userManager,
            Microsoft.Extensions.Options.Options.Create(_passkeyOptions),
            _clock,
            NullLogger<AspNetPasskeyService>.Instance);
    }

    // ────────────────────── GetPasskeysAsync ──────────────────────

    [Fact]
    public async Task GetPasskeysAsync_UserExists_ReturnsMappedPasskeys()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();
        byte[] credentialId = new byte[32];
        credentialId[0] = 0x01;

        UserPasskeyInfo passkey = CreatePasskeyInfo(credentialId);
        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo> { passkey });

        IReadOnlyList<PasskeyInfo> result = await _sut.GetPasskeysAsync(
            userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(1);
        result[0].CreatedAt.ShouldBe(FixedNow);
        result[0].LastUsedAt.ShouldBeNull();
        result[0].Name.ShouldBeNull();
    }

    [Fact]
    public async Task GetPasskeysAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((GranitUser?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.GetPasskeysAsync(unknownId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task GetPasskeysAsync_NullOrWhiteSpaceUserId_ThrowsArgument(string? userId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.GetPasskeysAsync(userId!, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task GetPasskeysAsync_MultiplePasskeys_ReturnsAll()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();
        byte[] credId1 = new byte[32];
        byte[] credId2 = new byte[32];
        credId1[0] = 0x01;
        credId2[0] = 0x02;

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(credId1),
            CreatePasskeyInfo(credId2),
        });

        IReadOnlyList<PasskeyInfo> result = await _sut.GetPasskeysAsync(
            userId, TestContext.Current.CancellationToken);

        result.Count.ShouldBe(2);
    }

    [Fact]
    public async Task GetPasskeysAsync_EmptyList_ReturnsEmpty()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>());

        IReadOnlyList<PasskeyInfo> result = await _sut.GetPasskeysAsync(
            userId, TestContext.Current.CancellationToken);

        result.ShouldBeEmpty();
    }

    // ────────────────────── BeginRegistrationAsync ──────────────────────

    [Fact]
    public async Task BeginRegistrationAsync_ReturnsValidJson()
    {
        var userId = Guid.NewGuid();

        string json = await _sut.BeginRegistrationAsync(
            userId.ToString(), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("challenge").GetString().ShouldNotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("rp").GetProperty("name").GetString().ShouldBe("Granit");
        doc.RootElement.GetProperty("rp").GetProperty("id").GetString().ShouldBe("example.com");
        doc.RootElement.GetProperty("attestation").GetString().ShouldBe("none");
        doc.RootElement.GetProperty("mediation").GetString().ShouldBe("conditional");
    }

    [Fact]
    public async Task BeginRegistrationAsync_ContainsUserInfo()
    {
        var userId = Guid.NewGuid();

        string json = await _sut.BeginRegistrationAsync(
            userId.ToString(), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        JsonElement userElement = doc.RootElement.GetProperty("user");
        userElement.GetProperty("name").GetString().ShouldBe(userId.ToString());
        userElement.GetProperty("displayName").GetString().ShouldBe(userId.ToString());
    }

    [Fact]
    public async Task BeginRegistrationAsync_ContainsPubKeyCredParams()
    {
        string json = await _sut.BeginRegistrationAsync(
            Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        JsonElement paramsArray = doc.RootElement.GetProperty("pubKeyCredParams");
        paramsArray.GetArrayLength().ShouldBe(2);
        paramsArray[0].GetProperty("alg").GetInt32().ShouldBe(-7);
        paramsArray[1].GetProperty("alg").GetInt32().ShouldBe(-257);
    }

    [Fact]
    public async Task BeginRegistrationAsync_UsesConfiguredTimeout()
    {
        _passkeyOptions.AuthenticatorTimeout = TimeSpan.FromMinutes(3);

        string json = await _sut.BeginRegistrationAsync(
            Guid.NewGuid().ToString(), TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        long timeout = doc.RootElement.GetProperty("timeout").GetInt64();
        timeout.ShouldBe((long)TimeSpan.FromMinutes(3).TotalMilliseconds);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task BeginRegistrationAsync_NullOrWhiteSpaceUserId_ThrowsArgument(string? userId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.BeginRegistrationAsync(userId!, TestContext.Current.CancellationToken));
    }

    // ────────────────────── CompleteRegistrationAsync ──────────────────────

    [Fact]
    public async Task CompleteRegistrationAsync_ValidInput_ReturnsPasskeyInfo()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);

        PasskeyInfo result = await _sut.CompleteRegistrationAsync(
            userId, "{\"type\":\"public-key\"}", "My Key", TestContext.Current.CancellationToken);

        result.Name.ShouldBe("My Key");
        result.CreatedAt.ShouldBe(FixedNow);
        result.LastUsedAt.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteRegistrationAsync_CallsAddOrUpdatePasskey()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);

        await _sut.CompleteRegistrationAsync(
            userId, "{\"type\":\"public-key\"}", null, TestContext.Current.CancellationToken);

        await _userManager.Received(1).AddOrUpdatePasskeyAsync(user, Arg.Any<UserPasskeyInfo>());
    }

    [Fact]
    public async Task CompleteRegistrationAsync_NullName_ReturnsNullName()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);

        PasskeyInfo result = await _sut.CompleteRegistrationAsync(
            userId, "{\"type\":\"public-key\"}", null, TestContext.Current.CancellationToken);

        result.Name.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteRegistrationAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((GranitUser?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.CompleteRegistrationAsync(
                unknownId, "{\"type\":\"public-key\"}", null, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CompleteRegistrationAsync_NullOrWhiteSpaceUserId_ThrowsArgument(string? userId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.CompleteRegistrationAsync(
                userId!, "{}", null, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CompleteRegistrationAsync_NullOrWhiteSpaceCredentialJson_ThrowsArgument(string? credentialJson)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.CompleteRegistrationAsync(
                Guid.NewGuid().ToString(), credentialJson!, null, TestContext.Current.CancellationToken));
    }

    // ────────────────────── BeginAssertionAsync ──────────────────────

    [Fact]
    public async Task BeginAssertionAsync_ReturnsValidJson()
    {
        string json = await _sut.BeginAssertionAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("challenge").GetString().ShouldNotBeNullOrWhiteSpace();
        doc.RootElement.GetProperty("rpId").GetString().ShouldBe("example.com");
        doc.RootElement.GetProperty("userVerification").GetString().ShouldBe("preferred");
        doc.RootElement.GetProperty("mediation").GetString().ShouldBe("conditional");
    }

    [Fact]
    public async Task BeginAssertionAsync_HasEmptyAllowCredentials()
    {
        string json = await _sut.BeginAssertionAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        JsonElement allowCredentials = doc.RootElement.GetProperty("allowCredentials");
        allowCredentials.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task BeginAssertionAsync_UsesConfiguredTimeout()
    {
        _passkeyOptions.AuthenticatorTimeout = TimeSpan.FromMinutes(2);

        string json = await _sut.BeginAssertionAsync(TestContext.Current.CancellationToken);

        using var doc = JsonDocument.Parse(json);
        long timeout = doc.RootElement.GetProperty("timeout").GetInt64();
        timeout.ShouldBe((long)TimeSpan.FromMinutes(2).TotalMilliseconds);
    }

    // ────────────────────── CompleteAssertionAsync ──────────────────────

    [Fact]
    public async Task CompleteAssertionAsync_ValidCredential_ReturnsSuccess()
    {
        GranitUser user = CreateUser();
        byte[] credentialId = new byte[32];
        credentialId[0] = 0xAA;
        string credentialIdBase64 = Convert.ToBase64String(credentialId);
        string credentialJson = $"{{\"id\":\"{credentialIdBase64}\"}}";

        _userManager.FindByPasskeyIdAsync(Arg.Is<byte[]>(b => b.Length == credentialId.Length))
            .Returns(user);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(credentialId),
        });

        GranitPasskeyAssertionResult result = await _sut.CompleteAssertionAsync(
            credentialJson, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeTrue();
        result.UserId.ShouldBe(user.Id.ToString());
    }

    [Fact]
    public async Task CompleteAssertionAsync_MissingIdProperty_ReturnsFailed()
    {
        string credentialJson = "{\"type\":\"public-key\"}";

        GranitPasskeyAssertionResult result = await _sut.CompleteAssertionAsync(
            credentialJson, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAssertionAsync_UserNotFoundForCredential_ReturnsFailed()
    {
        byte[] credentialId = new byte[32];
        string credentialIdBase64 = Convert.ToBase64String(credentialId);
        string credentialJson = $"{{\"id\":\"{credentialIdBase64}\"}}";

        _userManager.FindByPasskeyIdAsync(Arg.Any<byte[]>()).Returns((GranitUser?)null);

        GranitPasskeyAssertionResult result = await _sut.CompleteAssertionAsync(
            credentialJson, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.UserId.ShouldBeNull();
    }

    [Fact]
    public async Task CompleteAssertionAsync_CredentialNotInUserPasskeys_ReturnsFailed()
    {
        GranitUser user = CreateUser();
        byte[] credentialId = new byte[32];
        credentialId[0] = 0xBB;
        string credentialIdBase64 = Convert.ToBase64String(credentialId);
        string credentialJson = $"{{\"id\":\"{credentialIdBase64}\"}}";

        // User is found by passkey lookup but the credential doesn't match stored ones
        _userManager.FindByPasskeyIdAsync(Arg.Any<byte[]>()).Returns(user);

        byte[] differentCredentialId = new byte[32];
        differentCredentialId[0] = 0xCC;
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(differentCredentialId),
        });

        GranitPasskeyAssertionResult result = await _sut.CompleteAssertionAsync(
            credentialJson, TestContext.Current.CancellationToken);

        result.Succeeded.ShouldBeFalse();
        result.UserId.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task CompleteAssertionAsync_NullOrWhiteSpaceCredentialJson_ThrowsArgument(string? credentialJson)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.CompleteAssertionAsync(credentialJson!, TestContext.Current.CancellationToken));
    }

    // ────────────────────── RenameAsync ──────────────────────

    [Fact]
    public async Task RenameAsync_ValidInput_CompletesWithoutException()
    {
        var passkeyId = Guid.NewGuid();

        await Should.NotThrowAsync(
            () => _sut.RenameAsync(
                Guid.NewGuid().ToString(), passkeyId, "New Name", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task RenameAsync_NullOrWhiteSpaceNewName_ThrowsArgument(string? newName)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.RenameAsync(
                Guid.NewGuid().ToString(), Guid.NewGuid(), newName!, TestContext.Current.CancellationToken));
    }

    // ────────────────────── DeleteAsync ──────────────────────

    [Fact]
    public async Task DeleteAsync_MultiplePasskeysAndNoPassword_Succeeds()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();
        var passkeyId = Guid.NewGuid();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(new byte[32]),
            CreatePasskeyInfo(new byte[32]),
        });
        _userManager.RemovePasskeyAsync(user, Arg.Any<byte[]>()).Returns(IdentityResult.Success);

        await Should.NotThrowAsync(
            () => _sut.DeleteAsync(userId, passkeyId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_LastPasskeyWithPassword_Succeeds()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();
        var passkeyId = Guid.NewGuid();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(true);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(new byte[32]),
        });
        _userManager.RemovePasskeyAsync(user, Arg.Any<byte[]>()).Returns(IdentityResult.Success);

        await Should.NotThrowAsync(
            () => _sut.DeleteAsync(userId, passkeyId, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_LastPasskeyWithoutPassword_ThrowsInvalidOperation()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();
        var passkeyId = Guid.NewGuid();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(new byte[32]),
        });

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(userId, passkeyId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("last passkey");
    }

    [Fact]
    public async Task DeleteAsync_RemovePasskeyFails_ThrowsInvalidOperation()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();
        var passkeyId = Guid.NewGuid();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(true);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>
        {
            CreatePasskeyInfo(new byte[32]),
            CreatePasskeyInfo(new byte[32]),
        });
        _userManager.RemovePasskeyAsync(user, Arg.Any<byte[]>())
            .Returns(IdentityResult.Failed(new IdentityError { Description = "Passkey not found" }));

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(userId, passkeyId, TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("Passkey not found");
    }

    [Fact]
    public async Task DeleteAsync_UserNotFound_ThrowsInvalidOperation()
    {
        string unknownId = Guid.NewGuid().ToString();
        _userManager.FindByIdAsync(unknownId).Returns((GranitUser?)null);

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(unknownId, Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain(unknownId);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task DeleteAsync_NullOrWhiteSpaceUserId_ThrowsArgument(string? userId)
    {
        await Should.ThrowAsync<ArgumentException>(
            () => _sut.DeleteAsync(userId!, Guid.NewGuid(), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task DeleteAsync_ZeroPasskeysWithoutPassword_ThrowsInvalidOperation()
    {
        GranitUser user = CreateUser();
        string userId = user.Id.ToString();

        _userManager.FindByIdAsync(userId).Returns(user);
        _userManager.HasPasswordAsync(user).Returns(false);
        _userManager.GetPasskeysAsync(user).Returns(new List<UserPasskeyInfo>());

        InvalidOperationException ex = await Should.ThrowAsync<InvalidOperationException>(
            () => _sut.DeleteAsync(userId, Guid.NewGuid(), TestContext.Current.CancellationToken));

        ex.Message.ShouldContain("last passkey");
    }

    // ────────────────────── Helpers ──────────────────────

    private static GranitUser CreateUser() => new()
    {
        Id = Guid.NewGuid(),
        UserName = "testuser",
        Email = "test@example.com",
    };

    private static UserPasskeyInfo CreatePasskeyInfo(byte[] credentialId) =>
        new(
            credentialId: credentialId,
            publicKey: new byte[65],
            createdAt: FixedNow,
            signCount: 0,
            transports: ["internal"],
            isUserVerified: true,
            isBackupEligible: true,
            isBackedUp: false,
            attestationObject: [],
            clientDataJson: []);
}
