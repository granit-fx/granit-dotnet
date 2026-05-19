using System.Security.Claims;
using Granit.Authorization;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.MultiTenancy.Authorization.Tests;

public sealed class PermissionBasedHostImpersonationGateTests
{
    [Fact]
    public async Task PermissionGranted_ReturnsAllow()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(
                MultiTenancyAuthorizationPermissions.Host.Impersonate,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var gate = new PermissionBasedHostImpersonationGate(checker);

        HostImpersonationDecision decision = await gate.CanImpersonateAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        decision.Allowed.ShouldBeTrue();
        decision.DenyReasonCode.ShouldBeNull();
    }

    [Fact]
    public async Task PermissionDenied_ReturnsDenyWithStableReasonCode()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(
                MultiTenancyAuthorizationPermissions.Host.Impersonate,
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(false));

        var gate = new PermissionBasedHostImpersonationGate(checker);

        HostImpersonationDecision decision = await gate.CanImpersonateAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        decision.Allowed.ShouldBeFalse();
        decision.DenyReasonCode.ShouldBe(HostImpersonationDecision.PermissionDenied);
    }

    [Fact]
    public async Task GateQueriesExactPermissionName()
    {
        // Pins the permission constant — a typo here would silently leave host
        // impersonation ungated.
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        checker.IsGrantedAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(true));

        var gate = new PermissionBasedHostImpersonationGate(checker);

        await gate.CanImpersonateAsync(
            new ClaimsPrincipal(new ClaimsIdentity()),
            Guid.NewGuid(),
            TestContext.Current.CancellationToken);

        await checker.Received(1).IsGrantedAsync(
            "MultiTenancy.Host.Impersonate",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void NullPrincipal_Throws()
    {
        IPermissionChecker checker = Substitute.For<IPermissionChecker>();
        var gate = new PermissionBasedHostImpersonationGate(checker);

        Should.Throw<ArgumentNullException>(async () =>
            await gate.CanImpersonateAsync(null!, Guid.NewGuid(), CancellationToken.None));
    }
}
