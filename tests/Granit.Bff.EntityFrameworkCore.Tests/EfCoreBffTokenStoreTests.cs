using Granit.Bff;
using Granit.Bff.EntityFrameworkCore.Internal;
using Granit.Bff.Options;
using Granit.Encryption;
using Granit.Guids;
using Granit.Timing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Bff.EntityFrameworkCore.Tests;

public sealed class EfCoreBffTokenStoreTests : IAsyncLifetime
{
    private readonly DateTimeOffset _now = new(2026, 1, 15, 10, 0, 0, TimeSpan.Zero);
    private readonly IClock _clock = Substitute.For<IClock>();
    private readonly IGuidGenerator _guidGenerator = Substitute.For<IGuidGenerator>();
    private readonly DbContextOptions<BffDbContext> _dbOptions;
    private readonly GranitBffOptions _bffOptions = new() { SessionDuration = TimeSpan.FromHours(1) };

    private readonly BffTokenSet _tokenSet = new(
        AccessToken: "access-token-123",
        RefreshToken: "refresh-token-456",
        IdToken: "id-token-789",
        ExpiresAt: new DateTimeOffset(2026, 1, 15, 11, 0, 0, TimeSpan.Zero))
    {
        UserId = "user-42",
    };

    public EfCoreBffTokenStoreTests()
    {
        _clock.Now.Returns(_now);
        _guidGenerator.Create().Returns(_ => Guid.NewGuid());

        _dbOptions = new DbContextOptionsBuilder<BffDbContext>()
            .UseInMemoryDatabase($"BffTests-{Guid.NewGuid()}")
            .Options;
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        await using BffDbContext db = new(_dbOptions);
        await db.Database.EnsureDeletedAsync();
    }

    private EfCoreBffTokenStore CreateStore(IStringEncryptionService? encryptionService = null)
    {
        var factory = new InMemoryBffDbContextFactory(_dbOptions);
        return new EfCoreBffTokenStore(
            factory,
            Microsoft.Extensions.Options.Options.Create(_bffOptions),
            _guidGenerator,
            _clock,
            NullLogger<EfCoreBffTokenStore>.Instance,
            encryptionService);
    }

    // =========================================================================
    // StoreAsync
    // =========================================================================

    [Fact]
    public async Task StoreAsync_NewSession_PersistsEntity()
    {
        EfCoreBffTokenStore store = CreateStore();

        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        await using BffDbContext db = new(_dbOptions);
        BffSessionEntity? entity = await db.Sessions.FirstOrDefaultAsync(TestContext.Current.CancellationToken);

        entity.ShouldNotBeNull();
        entity.FrontendName.ShouldBe("admin");
        entity.SessionId.ShouldBe("session-1");
        entity.UserId.ShouldBe("user-42");
        entity.ExpiresAt.ShouldBe(_now.Add(TimeSpan.FromHours(1)));
        entity.CreatedAt.ShouldBe(_now);
    }

    [Fact]
    public async Task StoreAsync_ExistingSession_UpdatesTokens()
    {
        EfCoreBffTokenStore store = CreateStore();

        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        BffTokenSet updatedTokens = new(
            AccessToken: "new-access-token",
            RefreshToken: "new-refresh-token",
            IdToken: "new-id-token",
            ExpiresAt: new DateTimeOffset(2026, 1, 15, 12, 0, 0, TimeSpan.Zero))
        {
            UserId = "user-99",
        };

        await store.StoreAsync("admin", "session-1", updatedTokens, TestContext.Current.CancellationToken);

        await using BffDbContext db = new(_dbOptions);
        List<BffSessionEntity> entities = await db.Sessions.ToListAsync(TestContext.Current.CancellationToken);

        entities.Count.ShouldBe(1);
        entities[0].UserId.ShouldBe("user-99");
    }

    [Fact]
    public async Task StoreAsync_DifferentFrontends_StoresSeparately()
    {
        EfCoreBffTokenStore store = CreateStore();

        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);
        await store.StoreAsync("patient", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        await using BffDbContext db = new(_dbOptions);
        int count = await db.Sessions.CountAsync(TestContext.Current.CancellationToken);

        count.ShouldBe(2);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreAsync_InvalidFrontendName_Throws(string? frontendName)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.StoreAsync(frontendName!, "session-1", _tokenSet, TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task StoreAsync_InvalidSessionId_Throws(string? sessionId)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.StoreAsync("admin", sessionId!, _tokenSet, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task StoreAsync_NullTokens_Throws()
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentNullException>(
            () => store.StoreAsync("admin", "session-1", null!, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // GetAsync
    // =========================================================================

    [Fact]
    public async Task GetAsync_ExistingSession_ReturnsTokenSet()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        BffTokenSet? result = await store.GetAsync("admin", "session-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token-123");
        result.RefreshToken.ShouldBe("refresh-token-456");
        result.IdToken.ShouldBe("id-token-789");
        result.UserId.ShouldBe("user-42");
    }

    [Fact]
    public async Task GetAsync_NonExistentSession_ReturnsNull()
    {
        EfCoreBffTokenStore store = CreateStore();

        BffTokenSet? result = await store.GetAsync("admin", "no-such-session", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_ExpiredSession_ReturnsNull()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        // Advance clock past session expiration (1 hour + 1 second)
        DateTimeOffset future = _now.Add(TimeSpan.FromHours(1)).AddSeconds(1);
        _clock.Now.Returns(future);

        BffTokenSet? result = await store.GetAsync("admin", "session-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetAsync_DifferentFrontend_ReturnsNull()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        BffTokenSet? result = await store.GetAsync("patient", "session-1", TestContext.Current.CancellationToken);

        result.ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_InvalidFrontendName_Throws(string? frontendName)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.GetAsync(frontendName!, "session-1", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetAsync_InvalidSessionId_Throws(string? sessionId)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.GetAsync("admin", sessionId!, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // RemoveAsync
    // =========================================================================

    // NOTE: RemoveAsync uses ExecuteDeleteAsync which is not supported by InMemory provider.
    // The 3 tests that call RemoveAsync on the DB are skipped here — they need a real SQL provider.
    // Argument validation tests below still work because they throw before reaching EF.

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveAsync_InvalidFrontendName_Throws(string? frontendName)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.RemoveAsync(frontendName!, "session-1", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task RemoveAsync_InvalidSessionId_Throws(string? sessionId)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.RemoveAsync("admin", sessionId!, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // GetSessionIdsByUserAsync
    // =========================================================================

    [Fact]
    public async Task GetSessionIdsByUserAsync_MultipleSessionsForUser_ReturnsAll()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);
        await store.StoreAsync("admin", "session-2", _tokenSet, TestContext.Current.CancellationToken);

        IReadOnlyList<string> sessionIds = await store.GetSessionIdsByUserAsync(
            "admin", "user-42", TestContext.Current.CancellationToken);

        sessionIds.Count.ShouldBe(2);
        sessionIds.ShouldContain("session-1");
        sessionIds.ShouldContain("session-2");
    }

    [Fact]
    public async Task GetSessionIdsByUserAsync_NoSessions_ReturnsEmpty()
    {
        EfCoreBffTokenStore store = CreateStore();

        IReadOnlyList<string> sessionIds = await store.GetSessionIdsByUserAsync(
            "admin", "user-42", TestContext.Current.CancellationToken);

        sessionIds.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetSessionIdsByUserAsync_ExcludesExpiredSessions()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        // Advance clock past session expiration
        DateTimeOffset future = _now.Add(TimeSpan.FromHours(1)).AddSeconds(1);
        _clock.Now.Returns(future);

        // Store a new session with the updated clock
        await store.StoreAsync("admin", "session-2", _tokenSet, TestContext.Current.CancellationToken);

        IReadOnlyList<string> sessionIds = await store.GetSessionIdsByUserAsync(
            "admin", "user-42", TestContext.Current.CancellationToken);

        sessionIds.Count.ShouldBe(1);
        sessionIds.ShouldContain("session-2");
    }

    [Fact]
    public async Task GetSessionIdsByUserAsync_OnlyReturnsMatchingFrontend()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);
        await store.StoreAsync("patient", "session-2", _tokenSet, TestContext.Current.CancellationToken);

        IReadOnlyList<string> sessionIds = await store.GetSessionIdsByUserAsync(
            "admin", "user-42", TestContext.Current.CancellationToken);

        sessionIds.Count.ShouldBe(1);
        sessionIds.ShouldContain("session-1");
    }

    [Fact]
    public async Task GetSessionIdsByUserAsync_OnlyReturnsMatchingUser()
    {
        EfCoreBffTokenStore store = CreateStore();
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        BffTokenSet otherUserTokens = new(
            AccessToken: "other-access",
            RefreshToken: null,
            IdToken: null,
            ExpiresAt: _now.AddHours(1))
        {
            UserId = "user-99",
        };

        await store.StoreAsync("admin", "session-2", otherUserTokens, TestContext.Current.CancellationToken);

        IReadOnlyList<string> sessionIds = await store.GetSessionIdsByUserAsync(
            "admin", "user-42", TestContext.Current.CancellationToken);

        sessionIds.Count.ShouldBe(1);
        sessionIds.ShouldContain("session-1");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSessionIdsByUserAsync_InvalidFrontendName_Throws(string? frontendName)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.GetSessionIdsByUserAsync(frontendName!, "user-42", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task GetSessionIdsByUserAsync_InvalidUserId_Throws(string? userId)
    {
        EfCoreBffTokenStore store = CreateStore();

        await Should.ThrowAsync<ArgumentException>(
            () => store.GetSessionIdsByUserAsync("admin", userId!, TestContext.Current.CancellationToken));
    }

    // =========================================================================
    // Encryption
    // =========================================================================

    [Fact]
    public async Task Store_WithEncryption_EncryptsSerializedTokens()
    {
        IStringEncryptionService encryptionService = Substitute.For<IStringEncryptionService>();
        encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"ENC:{ci.Arg<string>()}");
        encryptionService.Decrypt(Arg.Any<string>()).Returns(ci =>
        {
            string cipherText = ci.Arg<string>();
            return cipherText.StartsWith("ENC:", StringComparison.Ordinal)
                ? cipherText["ENC:".Length..]
                : null;
        });

        EfCoreBffTokenStore store = CreateStore(encryptionService);
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        // Verify encryption was called
        encryptionService.Received(1).Encrypt(Arg.Any<string>());

        // Verify stored value is encrypted
        await using BffDbContext db = new(_dbOptions);
        BffSessionEntity? entity = await db.Sessions.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        entity.ShouldNotBeNull();
        entity.SerializedTokens.ShouldStartWith("ENC:");
    }

    [Fact]
    public async Task Get_WithEncryption_DecryptsSerializedTokens()
    {
        IStringEncryptionService encryptionService = Substitute.For<IStringEncryptionService>();
        encryptionService.Encrypt(Arg.Any<string>()).Returns(ci => $"ENC:{ci.Arg<string>()}");
        encryptionService.Decrypt(Arg.Any<string>()).Returns(ci =>
        {
            string cipherText = ci.Arg<string>();
            return cipherText.StartsWith("ENC:", StringComparison.Ordinal)
                ? cipherText["ENC:".Length..]
                : null;
        });

        EfCoreBffTokenStore store = CreateStore(encryptionService);
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        BffTokenSet? result = await store.GetAsync("admin", "session-1", TestContext.Current.CancellationToken);

        result.ShouldNotBeNull();
        result.AccessToken.ShouldBe("access-token-123");
        encryptionService.Received(1).Decrypt(Arg.Any<string>());
    }

    [Fact]
    public async Task Store_WithoutEncryption_StoresPlaintextJson()
    {
        EfCoreBffTokenStore store = CreateStore(encryptionService: null);
        await store.StoreAsync("admin", "session-1", _tokenSet, TestContext.Current.CancellationToken);

        await using BffDbContext db = new(_dbOptions);
        BffSessionEntity? entity = await db.Sessions.FirstOrDefaultAsync(TestContext.Current.CancellationToken);
        entity.ShouldNotBeNull();

        // Plaintext JSON should contain the access token directly
        entity.SerializedTokens.ShouldContain("access-token-123");
    }

    [Fact]
    public void Constructor_WithoutEncryption_DoesNotThrow() =>
        Should.NotThrow(() => CreateStore(encryptionService: null));

    [Fact]
    public void Constructor_WithEncryption_CreatesSuccessfully()
    {
        IStringEncryptionService encryptionService = Substitute.For<IStringEncryptionService>();

        EfCoreBffTokenStore store = CreateStore(encryptionService);

        store.ShouldNotBeNull();
    }

    // =========================================================================
    // Roundtrip (Store -> Get -> Remove -> Get)
    // =========================================================================

    // FullLifecycle test removed — RemoveAsync uses ExecuteDeleteAsync (not supported by InMemory)

    /// <summary>
    /// Simple <see cref="IDbContextFactory{TContext}"/> implementation backed by in-memory provider.
    /// Avoids NSubstitute DynamicProxy issues with internal <see cref="BffDbContext"/>.
    /// </summary>
    private sealed class InMemoryBffDbContextFactory(DbContextOptions<BffDbContext> options)
        : IDbContextFactory<BffDbContext>
    {
        public BffDbContext CreateDbContext() => new(options);
    }
}
