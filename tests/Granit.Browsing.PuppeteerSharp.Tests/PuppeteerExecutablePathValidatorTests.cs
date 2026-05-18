using System.IO;
using Granit.Browsing.Exceptions;
using Granit.Browsing.PuppeteerSharp.Internal;
using Granit.Browsing.Sandbox;
using Shouldly;
using Xunit;

namespace Granit.Browsing.PuppeteerSharp.Tests;

public sealed class PuppeteerExecutablePathValidatorTests
{
    [Fact]
    public void Null_executable_returns_null()
        => PuppeteerExecutablePathValidator.Validate(null, "/usr/lib/chromium/").ShouldBeNull();

    [Fact]
    public void Null_prefix_returns_resolved_path()
    {
        string path = Path.GetTempFileName();
        try
        {
            string? result = PuppeteerExecutablePathValidator.Validate(path, null);
            result.ShouldBe(Path.GetFullPath(path));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void Path_outside_prefix_throws_sandbox_violation()
    {
        SandboxViolationException ex = Should.Throw<SandboxViolationException>(
            () => PuppeteerExecutablePathValidator.Validate("/tmp/evil-chrome", "/usr/lib/chromium/"));
        ex.Kind.ShouldBe(SandboxViolationKind.ExecutablePathRejected);
    }

    [Fact]
    public void Path_under_prefix_is_accepted()
    {
        string prefix = Path.Combine(Path.GetTempPath(), "granit-test-prefix");
        Directory.CreateDirectory(prefix);
        string exe = Path.Combine(prefix, "chrome");
        File.WriteAllText(exe, string.Empty);
        try
        {
            string? result = PuppeteerExecutablePathValidator.Validate(exe, prefix);
            result.ShouldBe(Path.GetFullPath(exe));
        }
        finally
        {
            File.Delete(exe);
            Directory.Delete(prefix);
        }
    }
}
