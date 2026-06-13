using Granit.Identity.Local.Handlers;
using Granit.Identity.Local.Services;
using Granit.MultiTenancy;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace Granit.Identity.Local.Tests.Handlers;

public sealed class DeniedSessionRemediationHandlerTests
{
    private readonly IIdentityUserReader _reader = Substitute.For<IIdentityUserReader>();
    private readonly IPasswordResetService _reset = Substitute.For<IPasswordResetService>();
    private readonly ICurrentTenant _tenant = Substitute.For<ICurrentTenant>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    public DeniedSessionRemediationHandlerTests() =>
        _tenant.Change(Arg.Any<Guid?>()).Returns(Substitute.For<IDisposable>());

    private Task InvokeAsync() =>
        DeniedSessionRemediationHandler.HandleAsync(
            new SessionDeniedEto("user-1", "s1", TenantId: null, DateTimeOffset.UnixEpoch),
            _reader, _reset, _tenant, NullLogger<DeniedSessionRemediationHandler>.Instance, Ct);

    [Fact]
    public async Task LocalUserWithEmail_RequestsPasswordReset()
    {
        IIdentityUser user = Substitute.For<IIdentityUser>();
        user.Email.Returns("user@example.com");
        _reader.GetUserAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);

        await InvokeAsync();

        await _reset.Received(1).RequestResetAsync("user@example.com", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UnknownUser_SkipsReset()
    {
        _reader.GetUserAsync("user-1", Arg.Any<CancellationToken>()).Returns((IIdentityUser?)null);

        await InvokeAsync();

        await _reset.DidNotReceiveWithAnyArgs().RequestResetAsync(default!, Ct);
    }

    [Fact]
    public async Task UserWithoutEmail_SkipsReset()
    {
        IIdentityUser user = Substitute.For<IIdentityUser>();
        user.Email.Returns((string?)null);
        _reader.GetUserAsync("user-1", Arg.Any<CancellationToken>()).Returns(user);

        await InvokeAsync();

        await _reset.DidNotReceiveWithAnyArgs().RequestResetAsync(default!, Ct);
    }
}
