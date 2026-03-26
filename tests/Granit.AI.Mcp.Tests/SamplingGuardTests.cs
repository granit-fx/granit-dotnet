using Granit.AI.Mcp.Internal;
using Granit.AI.Mcp.Options;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Shouldly;
using MsOptions = Microsoft.Extensions.Options.Options;

namespace Granit.AI.Mcp.Tests;

public sealed class SamplingGuardTests
{
    [Fact]
    public void TryValidate_WhenDisabled_ShouldReject()
    {
        IOptions<GranitAIMcpOptions> options = MsOptions.Create(new GranitAIMcpOptions { EnableSampling = false });
        SamplingGuard sut = new(options, NullLogger<SamplingGuard>.Instance);

        bool result = sut.TryValidate(requestedMaxTokens: 100, serverOrigin: "test", out string? reason);

        result.ShouldBeFalse();
        reason.ShouldNotBeNull();
        reason.ShouldContain("disabled");
    }

    [Fact]
    public void TryValidate_WhenEnabled_ShouldAllow()
    {
        IOptions<GranitAIMcpOptions> options = MsOptions.Create(new GranitAIMcpOptions
        {
            EnableSampling = true,
            SamplingMaxTokensPerRequest = 2000,
        });
        SamplingGuard sut = new(options, NullLogger<SamplingGuard>.Instance);

        bool result = sut.TryValidate(requestedMaxTokens: 100, serverOrigin: "test", out string? reason);

        result.ShouldBeTrue();
        reason.ShouldBeNull();
    }

    [Fact]
    public void TryValidate_WhenTokensExceedLimit_ShouldReject()
    {
        IOptions<GranitAIMcpOptions> options = MsOptions.Create(new GranitAIMcpOptions
        {
            EnableSampling = true,
            SamplingMaxTokensPerRequest = 500,
        });
        SamplingGuard sut = new(options, NullLogger<SamplingGuard>.Instance);

        bool result = sut.TryValidate(requestedMaxTokens: 5000, serverOrigin: "test", out string? reason);

        result.ShouldBeFalse();
        reason.ShouldNotBeNull();
        reason.ShouldContain("5000");
        reason.ShouldContain("500");
    }

    [Fact]
    public void TryValidate_WhenTokenLimitIsZero_ShouldAllowUnlimited()
    {
        IOptions<GranitAIMcpOptions> options = MsOptions.Create(new GranitAIMcpOptions
        {
            EnableSampling = true,
            SamplingMaxTokensPerRequest = 0,
        });
        SamplingGuard sut = new(options, NullLogger<SamplingGuard>.Instance);

        bool result = sut.TryValidate(requestedMaxTokens: 100_000, serverOrigin: "test", out string? reason);

        result.ShouldBeTrue();
        reason.ShouldBeNull();
    }

    [Fact]
    public void TryValidate_WhenNullTokens_ShouldAllow()
    {
        IOptions<GranitAIMcpOptions> options = MsOptions.Create(new GranitAIMcpOptions
        {
            EnableSampling = true,
            SamplingMaxTokensPerRequest = 2000,
        });
        SamplingGuard sut = new(options, NullLogger<SamplingGuard>.Instance);

        bool result = sut.TryValidate(requestedMaxTokens: null, serverOrigin: null, out string? reason);

        result.ShouldBeTrue();
        reason.ShouldBeNull();
    }
}
