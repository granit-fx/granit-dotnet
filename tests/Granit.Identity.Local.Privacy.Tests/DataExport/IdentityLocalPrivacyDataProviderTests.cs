using Granit.Domain.ValueObjects;
using Granit.Identity.Local.Domain;
using Granit.Identity.Local.Privacy.DataExport;
using Granit.Privacy.BlobStorage;
using Granit.Privacy.DataExport;
using Granit.Privacy.DataExport.Fragments;
using Microsoft.AspNetCore.Identity;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Local.Privacy.Tests.DataExport;

public sealed class IdentityLocalPrivacyDataProviderTests
{
    private readonly IStagedFragmentBuilder _builder = Substitute.For<IStagedFragmentBuilder>();

    private static UserManager<LocalIdentity> CreateUserManager(IUserStore<LocalIdentity> store) =>
        Substitute.For<UserManager<LocalIdentity>>(store, null, null, null, null, null, null, null, null);

    private static PrivacyExportContext Ctx(Guid? subjectUserId = null) =>
        new(
            RequestId: Guid.NewGuid(),
            SubjectUserId: subjectUserId ?? Guid.NewGuid(),
            CallerUserId: subjectUserId ?? Guid.NewGuid(),
            TenantId: null,
            Regulation: "EU_GDPR");

    private static StagedExportFragment StubFragment() =>
        new()
        {
            EntryPath = "identity-local.json",
            ContentType = "application/json",
            IntegrityTag = "v1:stub",
            StagedBlob = BlobReference.Create(Guid.NewGuid().ToString()),
        };

    [Fact]
    public void ProviderName_Is_IdentityLocal() =>
        IdentityLocalPrivacyDataProvider.ProviderName.ShouldBe("identity-local");

    [Fact]
    public void DisplayKey_TargetsScopeSelectorLocKey() =>
        IdentityLocalPrivacyDataProvider.DisplayKey.ShouldBe("Privacy.Scopes.IdentityLocal");

    [Fact]
    public void FeatureName_IsNull_AlwaysVisible() =>
        IdentityLocalPrivacyDataProvider.FeatureName.ShouldBeNull();

    [Fact]
    public async Task HasDataAsync_ReturnsFalse_WhenUserNotFound()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        UserManager<LocalIdentity> userManager = CreateUserManager(store);
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);

        IdentityLocalPrivacyDataProvider sut = new(userManager, _builder);

        bool has = await sut.HasDataAsync(Ctx(), TestContext.Current.CancellationToken);

        has.ShouldBeFalse();
    }

    [Fact]
    public async Task HasDataAsync_ReturnsTrue_WhenUserExists()
    {
        var userId = Guid.NewGuid();
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        UserManager<LocalIdentity> userManager = CreateUserManager(store);
        userManager.FindByIdAsync(userId.ToString())
            .Returns(new LocalIdentity { Id = userId, UserName = "alice", CreatedBy = "system" });

        IdentityLocalPrivacyDataProvider sut = new(userManager, _builder);

        bool has = await sut.HasDataAsync(Ctx(userId), TestContext.Current.CancellationToken);

        has.ShouldBeTrue();
    }

    [Fact]
    public async Task ExportAsync_UnknownUser_YieldsNothing()
    {
        IUserStore<LocalIdentity> store = Substitute.For<IUserStore<LocalIdentity>>();
        UserManager<LocalIdentity> userManager = CreateUserManager(store);
        userManager.FindByIdAsync(Arg.Any<string>()).Returns((LocalIdentity?)null);

        IdentityLocalPrivacyDataProvider sut = new(userManager, _builder);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in sut.ExportAsync(Ctx(), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.ShouldBeEmpty();
        await _builder.DidNotReceive().BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<object>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ExportAsync_KnownUser_BuildsDtoAndYieldsOneFragment()
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

        IdentityLocalExportResponse? capturedDto = null;
        _builder.BuildJsonAsync(
            Arg.Any<PrivacyExportContext>(),
            IdentityLocalPrivacyDataProvider.ProviderName,
            "identity-local.json",
            Arg.Do<IdentityLocalExportResponse>(d => capturedDto = d),
            Arg.Any<CancellationToken>())
            .Returns(StubFragment());

        IdentityLocalPrivacyDataProvider sut = new(userManager, _builder);

        List<ExportFragment> fragments = [];
        await foreach (ExportFragment f in sut.ExportAsync(Ctx(userId), TestContext.Current.CancellationToken))
        {
            fragments.Add(f);
        }

        fragments.Count.ShouldBe(1);
        fragments[0].ShouldBeOfType<StagedExportFragment>();
        capturedDto.ShouldNotBeNull();
        capturedDto!.Id.ShouldBe(userId);
        capturedDto.Email.ShouldBe("alice@example.com");
        capturedDto.FirstName.ShouldBe("Alice");
        capturedDto.Roles.ShouldBe(["Admin", "User"]);
    }
}
