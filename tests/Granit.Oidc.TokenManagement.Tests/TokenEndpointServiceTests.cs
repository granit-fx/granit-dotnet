using System.Diagnostics.Metrics;
using Granit.Oidc.Discovery;
using Granit.Oidc.DPoP;
using Granit.Oidc.TokenManagement.Diagnostics;
using Granit.Oidc.TokenManagement.Services;
using Granit.Oidc.TokenManagement.Services.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests;

public sealed class TokenEndpointServiceTests
{
    [Fact]
    public void TokenEndpointService_CanBeConstructed()
    {
        IDiscoveryDocumentService discoveryDocumentService = Substitute.For<IDiscoveryDocumentService>();
        IHttpClientFactory httpClientFactory = Substitute.For<IHttpClientFactory>();
        IDPoPProofService dpopProofService = Substitute.For<IDPoPProofService>();
        TestMeterFactory meterFactory = new();
        TokenManagementMetrics metrics = new(meterFactory);

        TokenEndpointService sut = new(
            discoveryDocumentService,
            httpClientFactory,
            dpopProofService,
            metrics,
            NullLogger<TokenEndpointService>.Instance);

        sut.ShouldNotBeNull();
        sut.ShouldBeAssignableTo<ITokenEndpointService>();
    }

    [Fact]
    public void DPoPOptions_Record_PropertiesWork()
    {
        DPoPOptions options = new("private-key-jwk", "server-nonce");

        options.PrivateKeyJwk.ShouldBe("private-key-jwk");
        options.Nonce.ShouldBe("server-nonce");
    }

    [Fact]
    public void DPoPOptions_WithExpression_CreatesNewInstance()
    {
        DPoPOptions original = new("key-1");
        DPoPOptions updated = original with { Nonce = "new-nonce" };

        updated.PrivateKeyJwk.ShouldBe("key-1");
        updated.Nonce.ShouldBe("new-nonce");
        original.Nonce.ShouldBeNull();
    }

    private sealed class TestMeterFactory : IMeterFactory
    {
        private readonly List<Meter> _meters = [];

        public Meter Create(MeterOptions options)
        {
            Meter meter = new(options);
            _meters.Add(meter);
            return meter;
        }

        public void Dispose()
        {
            foreach (Meter meter in _meters)
            {
                meter.Dispose();
            }

            _meters.Clear();
        }
    }
}
