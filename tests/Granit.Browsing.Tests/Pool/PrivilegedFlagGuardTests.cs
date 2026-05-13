using System.Collections.Generic;
using Granit.Browsing.Exceptions;
using Granit.Browsing.Pool;
using Granit.Browsing.Sandbox;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Pool;

public sealed class PrivilegedFlagGuardTests
{
    [Theory]
    // disableSandbox × containsNoSandboxArg × optIn × container × root → expected pass/throw
    // baseline: no flags at all → always passes.
    [InlineData(false, false, false, false, false, true)]
    // disable sandbox + nothing else → refused.
    [InlineData(true, false, false, false, false, false)]
    // --no-sandbox arg + nothing else → refused.
    [InlineData(false, true, false, false, false, false)]
    // disable sandbox + opt-in + container + non-root → permitted.
    [InlineData(true, false, true, true, false, true)]
    // disable sandbox + opt-in + non-container → refused (not in container).
    [InlineData(true, false, true, false, false, false)]
    // disable sandbox + opt-in + container + root → refused.
    [InlineData(true, false, true, true, true, false)]
    // arg + opt-in + container + non-root → permitted.
    [InlineData(false, true, true, true, false, true)]
    public void EnsureSafe_should_enforce_matrix(
        bool disableSandbox,
        bool containsNoSandboxArg,
        bool optIn,
        bool container,
        bool root,
        bool expectedPass)
    {
        FakeProbe probe = new(optIn, container, root);
        List<string>? extra = containsNoSandboxArg ? ["--no-sandbox"] : null;

        if (expectedPass)
        {
            Should.NotThrow(() => PrivilegedFlagGuard.EnsureSafe(
                disableSandbox, extra, NullLogger.Instance, probe));
        }
        else
        {
            Should.Throw<SandboxViolationException>(() => PrivilegedFlagGuard.EnsureSafe(
                disableSandbox, extra, NullLogger.Instance, probe))
                .Kind.ShouldBe(SandboxViolationKind.PrivilegedFlagRefused);
        }
    }

    [Fact]
    public void EnsureSafe_should_detect_disable_setuid_sandbox_arg()
    {
        FakeProbe probe = new(optIn: false, container: false, root: false);

        Should.Throw<SandboxViolationException>(() => PrivilegedFlagGuard.EnsureSafe(
            disableSandbox: false,
            extraArgs: ["--disable-setuid-sandbox"],
            NullLogger.Instance,
            probe));
    }

    [Theory]
    [InlineData("--disable-web-security")]
    [InlineData("--allow-file-access-from-files")]
    [InlineData("--disable-features=IsolateOrigins")]
    [InlineData("--disable-site-isolation-trials")]
    [InlineData("--remote-debugging-port=9222")]
    [InlineData("--remote-debugging-address=0.0.0.0")]
    [InlineData("--proxy-server=http://attacker.example/")]
    [InlineData("--proxy-bypass-list=*")]
    [InlineData("--ignore-certificate-errors")]
    [InlineData("--user-data-dir=/tmp/x")]
    public void EnsureSafe_should_refuse_always_forbidden_flag_unconditionally(string arg)
    {
        // Even with a fully-vetted container + opt-in + non-root environment, the
        // always-forbidden flags must be refused — they widen the renderer attack
        // surface in ways that no opt-in mitigates.
        FakeProbe probe = new(optIn: true, container: true, root: false);

        Should.Throw<SandboxViolationException>(() => PrivilegedFlagGuard.EnsureSafe(
            disableSandbox: false,
            extraArgs: [arg],
            NullLogger.Instance,
            probe))
            .Kind.ShouldBe(SandboxViolationKind.PrivilegedFlagRefused);
    }

    [Fact]
    public void EnsureSafe_should_not_match_substring_inside_other_flag()
    {
        // --js-flags=--no-sandbox is a Node-only knob; the OUTER flag is --js-flags
        // and the inner --no-sandbox is an argument to V8, not to Chromium. Exact-token
        // match must NOT confuse it with the sandbox-disable flag.
        FakeProbe probe = new(optIn: false, container: false, root: false);

        Should.NotThrow(() => PrivilegedFlagGuard.EnsureSafe(
            disableSandbox: false,
            extraArgs: ["--js-flags=--no-sandbox"],
            NullLogger.Instance,
            probe));
    }

    [Fact]
    public void EnsureSafe_should_match_no_sandbox_with_trailing_equals_value()
    {
        // Bare --no-sandbox and --no-sandbox=true both refer to the sandbox knob.
        FakeProbe probe = new(optIn: false, container: false, root: false);

        Should.Throw<SandboxViolationException>(() => PrivilegedFlagGuard.EnsureSafe(
            disableSandbox: false,
            extraArgs: ["--no-sandbox=true"],
            NullLogger.Instance,
            probe));
    }

    private sealed class FakeProbe(bool optIn, bool container, bool root) : IEnvironmentProbe
    {
        public string? GetEnvironmentVariable(string name) =>
            name == PrivilegedFlagGuard.OptInEnvVar ? (optIn ? "1" : null) : null;

        public bool IsRunningInContainer() => container;

        public bool IsRunningAsRoot() => root;
    }
}
