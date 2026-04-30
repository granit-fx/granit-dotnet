using Granit.Entities.Endpoints.Internal;
using Shouldly;
using Xunit;

namespace Granit.Entities.Endpoints.Tests;

public sealed class EntityManifestETagTests
{
    [Fact]
    public void Compute_returns_quoted_strong_etag()
    {
        string etag = EntityManifestETag.Compute(new { name = "Granit.Parties.Party" });

        etag.ShouldStartWith("\"");
        etag.ShouldEndWith("\"");
        etag.Length.ShouldBe(34); // 32 hex chars + 2 quotes
    }

    [Fact]
    public void Compute_is_deterministic_for_equal_payloads()
    {
        var a = new { name = "X", value = 42 };
        var b = new { name = "X", value = 42 };

        EntityManifestETag.Compute(a).ShouldBe(EntityManifestETag.Compute(b));
    }

    [Fact]
    public void Compute_differs_when_payload_differs()
    {
        var a = new { name = "X" };
        var b = new { name = "Y" };

        EntityManifestETag.Compute(a).ShouldNotBe(EntityManifestETag.Compute(b));
    }

    [Fact]
    public void Compute_throws_on_null()
    {
        Should.Throw<ArgumentNullException>(() => EntityManifestETag.Compute<object>(null!));
    }
}
