using Granit.Oidc.DPoP;
using Granit.Oidc.TokenManagement.DPoP.Internal;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Oidc.TokenManagement.Tests.DPoP;

public sealed class DPoPKeyStoreTests
{
    private readonly IDPoPProofService _proofService = Substitute.For<IDPoPProofService>();

    [Fact]
    public void GetOrCreateKey_SameClient_ReturnsStableKey()
    {
        _proofService.GenerateKeyPair().Returns("key-a-1", "key-a-2");
        DPoPKeyStore store = new(_proofService);

        string first = store.GetOrCreateKey("client-a");
        string second = store.GetOrCreateKey("client-a");

        // Stability across calls is what keeps DPoP-bound tokens valid past a
        // handler rotation: the key must NOT change for a given client.
        second.ShouldBe(first);
        _proofService.Received(1).GenerateKeyPair();
    }

    [Fact]
    public void GetOrCreateKey_DifferentClients_ReturnsDistinctKeys()
    {
        _proofService.GenerateKeyPair().Returns("key-a", "key-b");
        DPoPKeyStore store = new(_proofService);

        string a = store.GetOrCreateKey("client-a");
        string b = store.GetOrCreateKey("client-b");

        a.ShouldNotBe(b);
        _proofService.Received(2).GenerateKeyPair();
    }

    [Fact]
    public void GetOrCreateKey_NullClientName_Throws()
    {
        DPoPKeyStore store = new(_proofService);

        Should.Throw<ArgumentNullException>(() => store.GetOrCreateKey(null!));
    }
}
