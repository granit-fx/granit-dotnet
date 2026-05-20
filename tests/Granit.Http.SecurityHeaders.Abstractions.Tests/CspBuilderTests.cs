using Shouldly;
using Xunit;

namespace Granit.Http.SecurityHeaders.Tests;

public sealed class CspBuilderTests
{
    [Fact]
    public void AddScriptSrc_StoresSources()
    {
        CspBuilder b = new();
        b.AddScriptSrc("'self'", "'unsafe-inline'");

        b.Directives.ShouldContainKey("script-src");
        b.Directives["script-src"].ShouldBe(["'self'", "'unsafe-inline'"], ignoreOrder: true);
    }

    [Fact]
    public void AddScriptSrc_DedupesIdenticalSources()
    {
        CspBuilder b = new();
        b.AddScriptSrc("'self'");
        b.AddScriptSrc("'self'", "'unsafe-inline'");

        b.Directives["script-src"].Count.ShouldBe(2);
        b.Directives["script-src"].ShouldContain("'self'");
        b.Directives["script-src"].ShouldContain("'unsafe-inline'");
    }

    [Fact]
    public void AddDirective_WithEmptyArray_DoesNotCreateEntry()
    {
        CspBuilder b = new();
        b.AddScriptSrc();

        b.Directives.ShouldNotContainKey("script-src");
    }

    [Fact]
    public void FluentApi_ReturnsSelf()
    {
        CspBuilder b = new();
        CspBuilder returned = b
            .AddDefaultSrc("'self'")
            .AddScriptSrc("'self'")
            .AddStyleSrc("'self'")
            .SetReportUri("/csp-report")
            .SetUpgradeInsecureRequests();

        returned.ShouldBeSameAs(b);
    }

    [Fact]
    public void SetReportUri_StoresValue()
    {
        CspBuilder b = new();
        b.SetReportUri("/csp-report");

        b.ReportUri.ShouldBe("/csp-report");
    }

    [Fact]
    public void SetReportUri_NullOrEmpty_Throws()
    {
        CspBuilder b = new();
        Should.Throw<ArgumentException>(() => b.SetReportUri(string.Empty));
    }

    [Fact]
    public void SetReportTo_StoresValue()
    {
        CspBuilder b = new();
        b.SetReportTo("default-group");

        b.ReportTo.ShouldBe("default-group");
    }

    [Fact]
    public void SetUpgradeInsecureRequests_DefaultsToTrue()
    {
        CspBuilder b = new();
        b.SetUpgradeInsecureRequests();

        b.UpgradeInsecureRequests.ShouldBeTrue();
    }

    [Fact]
    public void SetUpgradeInsecureRequests_Disable_Works()
    {
        CspBuilder b = new();
        b.SetUpgradeInsecureRequests();
        b.SetUpgradeInsecureRequests(enabled: false);

        b.UpgradeInsecureRequests.ShouldBeFalse();
    }

    [Fact]
    public void Directives_ReturnsSnapshot()
    {
        // Snapshot semantics: a directive added after the snapshot is read
        // must not appear in the snapshot. (Caller does not see live state.)
        CspBuilder b = new();
        b.AddScriptSrc("'self'");

        IReadOnlyDictionary<string, IReadOnlyCollection<string>> snapshot = b.Directives;
        b.AddScriptSrc("'unsafe-inline'");

        snapshot["script-src"].Count.ShouldBe(1);
    }

    [Fact]
    public void AllDirectiveMethods_AreExercised()
    {
        // Smoke: every Add* method writes to a distinct directive key. If a
        // refactor accidentally points two methods at the same directive,
        // this catches the drift.
        CspBuilder b = new();
        b.AddDefaultSrc("'self'");
        b.AddScriptSrc("'self'");
        b.AddScriptSrcElem("'self'");
        b.AddScriptSrcAttr("'self'");
        b.AddStyleSrc("'self'");
        b.AddStyleSrcElem("'self'");
        b.AddStyleSrcAttr("'self'");
        b.AddFontSrc("'self'");
        b.AddImgSrc("'self'");
        b.AddConnectSrc("'self'");
        b.AddFrameSrc("'self'");
        b.AddWorkerSrc("'self'");
        b.AddMediaSrc("'self'");
        b.AddObjectSrc("'self'");
        b.AddManifestSrc("'self'");
        b.AddChildSrc("'self'");
        b.AddBaseUri("'self'");
        b.AddFormAction("'self'");
        b.AddFrameAncestors("'self'");

        b.Directives.Count.ShouldBe(19);
    }
}
