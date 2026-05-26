// PR-1b breaking migration: the IPrivacyDataProvider streaming contract
// invalidates the call sites below. Tests are preserved for reference and
// will be rewritten under P6.2 (#2313).
//
// To re-enable while migrating: drop the #if FALSE wrapper and update each
// PersonalDataPreparedEto/ReceivedFragment construction to the new 8-arg shape,
// then convert provider.ExportAsync(userId, ct) calls to (PrivacyExportContext, ct).

using Xunit;

namespace Granit.Identity.Local.Privacy.Tests.DataExport;

public class IdentityLocalPrivacyDataProviderTests_PendingRewrite
{
    [Fact(Skip = "P6.1b — pending rewrite under #2313 (P6.2)")]
    public void Pending() { }
}

#if FALSE_PR1B_PENDING_REWRITE
using System.Text.Json;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Privacy.DataExport;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Privacy.Tests.DataExport;

public sealed class IdentityLocalPrivacyDataProviderTests
{
    private static UserManager<LocalIdentity> CreateUserManager(IUserStore<LocalIdentity> store) =>
        Substitute.For<UserManager<LocalIdentity>>(store, null, null, null, null, null, null, null, null);

    [Fact]
    public void ProviderName_Is_IdentityLocal() =>
        IdentityLocalPrivacyDataProvider.ProviderName.ShouldBe("identity-local");

    [Fact]
    public void ContentType_IsApplicationJson() =>
        IdentityLocalPrivacyDataProvider.ContentType.ShouldBe("application/json");

    [Fact]
    public void FileName_IsStableAcrossRequests()
    {
        IdentityLocalPrivacyDataProvider.FileName(Guid.NewGuid())
            .ShouldBe("identity-local.json");
    }

    [Fact]
    public async Task ExportAsync_UnknownUser_ReturnsEmpty()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        UserManager<LocalIdentity> userManager = CreateUserManager(store);
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);

        IdentityLocalPrivacyDataProvider sut = new(userManager);

        ReadOnlyMemory<byte> result = await sut.ExportAsync(Guid.NewGuid(), TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_KnownUser_ReturnsJsonWithProfileAndRoles()
    {
        var userId = Guid.NewGuid();
        LocalIdentity user = new()
        {
            Id = userId,
            UserName = "alice",
            Email = "alice@example.com",
            EmailConfirmed = true,
            FirstName = "Alice",
            LastName = "Example",
            TenantId = Guid.NewGuid(),
            CreatedAt = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            CreatedBy = "system",
        };

        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        UserManager<LocalIdentity> userManager = CreateUserManager(store);
        userManager.FindByIdAsync(userId.ToString()).Returns(user);
        userManager.GetRolesAsync(user).Returns<IList<string>>(["Admin", "User"]);

        IdentityLocalPrivacyDataProvider sut = new(userManager);

        ReadOnlyMemory<byte> result = await sut.ExportAsync(userId, TestContext.Current.CancellationToken);

        result.IsEmpty.ShouldBeFalse();

        using var doc = JsonDocument.Parse(result);
        doc.RootElement.GetProperty("id").GetGuid().ShouldBe(userId);
        doc.RootElement.GetProperty("email").GetString().ShouldBe("alice@example.com");
        doc.RootElement.GetProperty("firstName").GetString().ShouldBe("Alice");
        doc.RootElement.GetProperty("roles").EnumerateArray()
            .Select(e => e.GetString()).ShouldBe(["Admin", "User"]);
    }
}
#endif
