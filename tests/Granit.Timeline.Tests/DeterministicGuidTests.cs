// =============================================================================
// Tests — DeterministicGuid (RFC 4122 v5)
// =============================================================================
// Pins the v5 implementation against the canonical "www.example.com" vector
// under the DNS namespace so endianness bugs surface immediately.
// =============================================================================

using Granit.Timeline.Internal;
using Shouldly;
using Xunit;

namespace Granit.Timeline.Tests;

public sealed class DeterministicGuidTests
{
    [Fact]
    public void CreateV5_MatchesRfc4122WwwExampleVector()
    {
        // RFC 4122 §Appendix B canonical vector — "www.example.com" under the
        // DNS namespace produces 2ed6657d-e927-568b-95e1-2665a8aea6a2.
        Guid dnsNamespace = new("6ba7b810-9dad-11d1-80b4-00c04fd430c8");

        Guid result = DeterministicGuid.CreateV5(dnsNamespace, "www.example.com");

        result.ShouldBe(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
    }

    [Fact]
    public void CreateV5_IsDeterministic()
    {
        var ns = Guid.NewGuid();
        Guid first = DeterministicGuid.CreateV5(ns, "stable-input");
        Guid second = DeterministicGuid.CreateV5(ns, "stable-input");
        first.ShouldBe(second);
    }

    [Fact]
    public void CreateV5_DiffersOnDifferentNames()
    {
        var ns = Guid.NewGuid();
        Guid a = DeterministicGuid.CreateV5(ns, "name-A");
        Guid b = DeterministicGuid.CreateV5(ns, "name-B");
        a.ShouldNotBe(b);
    }
}
