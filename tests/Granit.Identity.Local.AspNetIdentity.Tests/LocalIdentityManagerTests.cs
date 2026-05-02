using Granit.Guids;
using Granit.Identity;
using Granit.Identity.Domain;
using Granit.Identity.Local.AspNetIdentity;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Options;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.AspNetIdentity.Tests;

public sealed class LocalIdentityManagerTests
{
    private static readonly GranitLockoutOptions DefaultOptions = new()
    {
        MaxFailedAccessAttempts = 5,
        BaseLockoutDuration = TimeSpan.FromMinutes(5),
        MaxLockoutDuration = TimeSpan.FromHours(2),
        ExponentialBase = 2.0,
    };

    // ──── ComputeLockoutDuration ────

    [Theory]
    [InlineData(1, 5)]       // 5 min
    [InlineData(2, 10)]      // 10 min
    [InlineData(3, 20)]      // 20 min
    [InlineData(4, 40)]      // 40 min
    [InlineData(5, 80)]      // 1h20
    [InlineData(6, 120)]     // 2h (cap)
    [InlineData(7, 120)]     // 2h (cap)
    [InlineData(10, 120)]    // 2h (cap)
    public void ComputeLockoutDuration_ExponentialBackoff(int consecutiveLockouts, int expectedMinutes)
    {
        LocalIdentityManager manager = CreateManager(DefaultOptions);

        TimeSpan duration = manager.ComputeLockoutDuration(consecutiveLockouts);

        duration.ShouldBe(TimeSpan.FromMinutes(expectedMinutes));
    }

    [Fact]
    public void ComputeLockoutDuration_ZeroLockouts_ReturnsBaseDuration()
    {
        LocalIdentityManager manager = CreateManager(DefaultOptions);

        TimeSpan duration = manager.ComputeLockoutDuration(0);

        duration.ShouldBe(TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void ComputeLockoutDuration_CustomOptions()
    {
        var options = new GranitLockoutOptions
        {
            BaseLockoutDuration = TimeSpan.FromMinutes(1),
            MaxLockoutDuration = TimeSpan.FromMinutes(30),
            ExponentialBase = 3.0,
        };
        LocalIdentityManager manager = CreateManager(options);

        // 1 * 3^0 = 1 min
        manager.ComputeLockoutDuration(1).ShouldBe(TimeSpan.FromMinutes(1));
        // 1 * 3^1 = 3 min
        manager.ComputeLockoutDuration(2).ShouldBe(TimeSpan.FromMinutes(3));
        // 1 * 3^2 = 9 min
        manager.ComputeLockoutDuration(3).ShouldBe(TimeSpan.FromMinutes(9));
        // 1 * 3^3 = 27 min
        manager.ComputeLockoutDuration(4).ShouldBe(TimeSpan.FromMinutes(27));
        // 1 * 3^4 = 81 → capped at 30 min
        manager.ComputeLockoutDuration(5).ShouldBe(TimeSpan.FromMinutes(30));
    }

    [Fact]
    public void ComputeLockoutDuration_VeryHighLockoutCount_DoesNotOverflow()
    {
        LocalIdentityManager manager = CreateManager(DefaultOptions);

        // 2^99 would overflow — should be capped safely
        TimeSpan duration = manager.ComputeLockoutDuration(100);

        duration.ShouldBe(DefaultOptions.MaxLockoutDuration);
    }

    // ──── CreateAsync — auto-create canonical User aggregate (ADR-051 B-step 2.5) ────

    [Fact]
    public async Task CreateAsync_GeneratesId_WhenLocalIdentityIdIsEmpty()
    {
        var generated = Guid.NewGuid();
        IGuidGenerator generator = Substitute.For<IGuidGenerator>();
        generator.Create().Returns(generated);

        IUserStore<LocalIdentity> store = StoreReturning(IdentityResult.Success);
        IUserDirectoryWriter writer = Substitute.For<IUserDirectoryWriter>();

        LocalIdentityManager manager = CreateManager(DefaultOptions, store, writer, generator);

        var user = new LocalIdentity { Email = "alice@example.com", UserName = "alice@example.com" };
        IdentityResult result = await manager.CreateAsync(user);

        result.Succeeded.ShouldBeTrue();
        user.Id.ShouldBe(generated);
        user.UserId.ShouldBe(generated);
    }

    [Fact]
    public async Task CreateAsync_AlignsUserIdWithId_WhenIdIsAlreadyAssigned()
    {
        var prefilled = Guid.NewGuid();
        IUserStore<LocalIdentity> store = StoreReturning(IdentityResult.Success);
        IUserDirectoryWriter writer = Substitute.For<IUserDirectoryWriter>();

        LocalIdentityManager manager = CreateManager(DefaultOptions, store, writer);

        var user = new LocalIdentity
        {
            Id = prefilled,
            Email = "bob@example.com",
            UserName = "bob@example.com",
        };
        await manager.CreateAsync(user);

        user.Id.ShouldBe(prefilled);
        user.UserId.ShouldBe(prefilled);
    }

    [Fact]
    public async Task CreateAsync_PersistsCanonicalUser_BeforeDelegatingToBase()
    {
        IUserStore<LocalIdentity> store = StoreReturning(IdentityResult.Success);
        IUserDirectoryWriter writer = Substitute.For<IUserDirectoryWriter>();

        LocalIdentityManager manager = CreateManager(DefaultOptions, store, writer);

        var user = new LocalIdentity
        {
            Id = Guid.NewGuid(),
            Email = "carol@example.com",
            UserName = "carol@example.com",
            FirstName = "Carol",
            LastName = "Doe",
        };
        await manager.CreateAsync(user);

        await writer.Received(1).CreateAsync(
            Arg.Is<User>(u =>
                u.Id == user.Id
                && u.Email == "carol@example.com"
                && u.DisplayName == "Carol Doe"),
            Arg.Any<CancellationToken>());
        await writer.DidNotReceive().DeleteAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_FallsBackToEmailAsDisplayName_WhenNamesAreMissing()
    {
        IUserStore<LocalIdentity> store = StoreReturning(IdentityResult.Success);
        IUserDirectoryWriter writer = Substitute.For<IUserDirectoryWriter>();

        LocalIdentityManager manager = CreateManager(DefaultOptions, store, writer);

        var user = new LocalIdentity
        {
            Id = Guid.NewGuid(),
            Email = "dave@example.com",
            UserName = "dave@example.com",
        };
        await manager.CreateAsync(user);

        await writer.Received(1).CreateAsync(
            Arg.Is<User>(u => u.DisplayName == "dave@example.com"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_CompensatesWithDelete_WhenBaseReturnsFailure()
    {
        // The compensating delete keeps the canonical directory clean
        // when the LocalIdentity insert fails (e.g. duplicate username,
        // password validation rejects the credential).
        IdentityError error = new() { Code = "DuplicateUserName", Description = "Already taken." };
        IUserStore<LocalIdentity> store = StoreReturning(IdentityResult.Failed(error));
        IUserDirectoryWriter writer = Substitute.For<IUserDirectoryWriter>();

        LocalIdentityManager manager = CreateManager(DefaultOptions, store, writer);

        var id = Guid.NewGuid();
        var user = new LocalIdentity
        {
            Id = id,
            Email = "eve@example.com",
            UserName = "eve@example.com",
        };
        IdentityResult result = await manager.CreateAsync(user);

        result.Succeeded.ShouldBeFalse();
        await writer.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_CompensatesWithDelete_WhenBaseThrows()
    {
        // A throw — e.g. transient store outage — must still leave the
        // canonical directory consistent.
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        store.CreateAsync(Arg.Any<LocalIdentity>(), Arg.Any<CancellationToken>())
            .Returns<Task<IdentityResult>>(_ => throw new InvalidOperationException("store offline"));

        IUserDirectoryWriter writer = Substitute.For<IUserDirectoryWriter>();
        LocalIdentityManager manager = CreateManager(DefaultOptions, store, writer);

        var id = Guid.NewGuid();
        var user = new LocalIdentity
        {
            Id = id,
            Email = "frank@example.com",
            UserName = "frank@example.com",
        };

        await Should.ThrowAsync<InvalidOperationException>(() => manager.CreateAsync(user));
        await writer.Received(1).DeleteAsync(id, Arg.Any<CancellationToken>());
    }

    // ──── Helper ────

    private static IUserStore<LocalIdentity> StoreReturning(IdentityResult result)
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        store.CreateAsync(Arg.Any<LocalIdentity>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(result));
        store.GetUserIdAsync(Arg.Any<LocalIdentity>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(call.Arg<LocalIdentity>().Id.ToString()));
        return store;
    }

    private static LocalIdentityManager CreateManager(
        GranitLockoutOptions lockoutOptions,
        IUserStore<LocalIdentity>? store = null,
        IUserDirectoryWriter? userDirectoryWriter = null,
        IGuidGenerator? guidGenerator = null)
    {
        IOptions<IdentityOptions> identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
        IPasswordHasher<LocalIdentity> hasher = Substitute.For<IPasswordHasher<LocalIdentity>>();
        ILogger<LocalIdentityManager> logger = Substitute.For<ILogger<LocalIdentityManager>>();

        return new LocalIdentityManager(
            store ?? Substitute.For<IUserStore<LocalIdentity>>(),
            identityOptions,
            hasher,
            [],
            [],
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            logger,
            Microsoft.Extensions.Options.Options.Create(lockoutOptions),
            userDirectoryWriter ?? Substitute.For<IUserDirectoryWriter>(),
            guidGenerator ?? Substitute.For<IGuidGenerator>());
    }
}
