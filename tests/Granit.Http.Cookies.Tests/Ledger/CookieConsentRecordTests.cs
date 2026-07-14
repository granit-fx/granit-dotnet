using Granit.Http.Cookies.Domain;

namespace Granit.Http.Cookies.Tests.Ledger;

/// <summary>Factory-contract tests for <see cref="CookieConsentRecord"/>.</summary>
public sealed class CookieConsentRecordTests
{
    private static readonly DateTimeOffset DecidedAt = new(2026, 7, 14, 9, 30, 0, TimeSpan.Zero);

    [Fact]
    public void Create_CopiesCategories_AndRemovesDuplicates()
    {
        // Act
        var record = CookieConsentRecord.Create(
            ["analytics", "analytics", "preferences"],
            ["marketing", "marketing"],
            CookieConsentMode.OptIn,
            "cookieconsent",
            DecidedAt);

        // Assert
        record.GrantedCategories.ShouldBe(["analytics", "preferences"]);
        record.DeniedCategories.ShouldBe(["marketing"]);
        record.Mode.ShouldBe(CookieConsentMode.OptIn);
        record.CmpSource.ShouldBe("cookieconsent");
        record.DecidedAt.ShouldBe(DecidedAt);
    }

    [Fact]
    public void Create_TruncatesUserAgent_ToMaxLength()
    {
        // Arrange
        string longUserAgent = new('a', CookieConsentRecord.MaxUserAgentLength + 100);

        // Act
        var record = CookieConsentRecord.Create(
            ["analytics"], [], CookieConsentMode.OptIn, "cmp", DecidedAt, userAgent: longUserAgent);

        // Assert
        record.UserAgent!.Length.ShouldBe(CookieConsentRecord.MaxUserAgentLength);
    }

    [Fact]
    public void Create_KeepsShortUserAgent_Untouched()
    {
        // Act
        var record = CookieConsentRecord.Create(
            ["analytics"], [], CookieConsentMode.OptIn, "cmp", DecidedAt, userAgent: "curl/8.5");

        // Assert
        record.UserAgent.ShouldBe("curl/8.5");
    }

    [Fact]
    public void Create_WithEmptyCmpSource_Throws() =>
        Should.Throw<ArgumentException>(() => CookieConsentRecord.Create(
            ["analytics"], [], CookieConsentMode.OptIn, string.Empty, DecidedAt));

    [Fact]
    public void Create_WithNullCategories_Throws() =>
        Should.Throw<ArgumentNullException>(() => CookieConsentRecord.Create(
            null!, [], CookieConsentMode.OptIn, "cmp", DecidedAt));

    [Fact]
    public void TenantId_IsInterceptorStamped_ViaExplicitInterface()
    {
        // Arrange
        var record = CookieConsentRecord.Create(
            ["analytics"], [], CookieConsentMode.OptIn, "cmp", DecidedAt);
        var tenantId = Guid.NewGuid();

        // Act — the AuditedEntityInterceptor writes through the explicit interface.
        ((IMultiTenant)record).TenantId = tenantId;

        // Assert
        record.TenantId.ShouldBe(tenantId);
    }
}
