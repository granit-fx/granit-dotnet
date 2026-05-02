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

public sealed class GranitUserManagerTests
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

    // ──── Helper ────

    private static LocalIdentityManager CreateManager(GranitLockoutOptions lockoutOptions)
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        IOptions<IdentityOptions> identityOptions = Microsoft.Extensions.Options.Options.Create(new IdentityOptions());
        IPasswordHasher<LocalIdentity> hasher = Substitute.For<IPasswordHasher<LocalIdentity>>();
        ILogger<LocalIdentityManager> logger = Substitute.For<ILogger<LocalIdentityManager>>();

        return new LocalIdentityManager(
            store,
            identityOptions,
            hasher,
            [],
            [],
            Substitute.For<ILookupNormalizer>(),
            new IdentityErrorDescriber(),
            Substitute.For<IServiceProvider>(),
            logger,
            Microsoft.Extensions.Options.Options.Create(lockoutOptions));
    }
}
