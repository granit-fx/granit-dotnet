using System;
using Granit.Browsing.Sandbox;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Sandbox;

public sealed class SandboxViolationExceptionTests
{
    [Fact]
    public void Kind_should_round_trip()
    {
        SandboxViolationException ex = new(SandboxViolationKind.CspBypassDenied, "denied");

        ex.Kind.ShouldBe(SandboxViolationKind.CspBypassDenied);
        ex.Message.ShouldBe("denied");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Inner_exception_should_be_preserved()
    {
        InvalidOperationException inner = new("inner");
        SandboxViolationException ex = new(SandboxViolationKind.ScriptInjectionDenied, "wrap", inner);

        ex.Kind.ShouldBe(SandboxViolationKind.ScriptInjectionDenied);
        ex.InnerException.ShouldBe(inner);
    }
}
