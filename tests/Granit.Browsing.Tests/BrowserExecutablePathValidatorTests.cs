using Granit.Browsing.Exceptions;
using Granit.Browsing.Sandbox;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests;

public sealed class BrowserExecutablePathValidatorTests
{
    private const string OptionName = "ExecutablePath";

    [Fact]
    public void Null_executable_returns_null()
        => BrowserExecutablePathValidator.Validate(null, "/usr/lib/chromium/", OptionName).ShouldBeNull();

    [Fact]
    public void Null_prefix_returns_resolved_path()
    {
        string path = Path.GetTempFileName();
        try
        {
            string? result = BrowserExecutablePathValidator.Validate(path, null, OptionName);
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
            () => BrowserExecutablePathValidator.Validate("/tmp/evil-chrome", "/usr/lib/chromium/", OptionName));
        ex.Kind.ShouldBe(SandboxViolationKind.ExecutablePathRejected);
    }

    [Fact]
    public void Path_under_prefix_is_accepted()
    {
        string prefix = Path.Combine(Path.GetTempPath(), "granit-browser-test-prefix");
        Directory.CreateDirectory(prefix);
        string exe = Path.Combine(prefix, "chrome");
        File.WriteAllText(exe, string.Empty);
        try
        {
            string? result = BrowserExecutablePathValidator.Validate(exe, prefix, OptionName);
            result.ShouldBe(Path.GetFullPath(exe));
        }
        finally
        {
            File.Delete(exe);
            Directory.Delete(prefix);
        }
    }
}
