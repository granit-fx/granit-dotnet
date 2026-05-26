using Granit.Domain.ValueObjects;
using Granit.Privacy.DataExport;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport;

public sealed class ReceivedFragmentTests
{
    [Fact]
    public void ReceivedFragment_Constructor_SetsAllProperties()
    {
        var blob = BlobReference.Create("blob-456");

        ReceivedFragment sut = new(
            ProviderName: "billing",
            FragmentKind: "staged",
            SourceContainer: "gdpr-exports",
            BlobReferenceId: blob,
            EntryPath: "billing.csv",
            ContentType: "text/csv",
            IntegrityTag: "v1:abc");

        sut.ProviderName.ShouldBe("billing");
        sut.FragmentKind.ShouldBe("staged");
        sut.SourceContainer.ShouldBe("gdpr-exports");
        sut.BlobReferenceId.ShouldBe(blob);
        sut.EntryPath.ShouldBe("billing.csv");
        sut.ContentType.ShouldBe("text/csv");
        sut.IntegrityTag.ShouldBe("v1:abc");
    }

    [Fact]
    public void ReceivedFragment_Equality_SameValues_AreEqual()
    {
        var blob = BlobReference.Create("ref");

        ReceivedFragment a = new("p", "staged", "gdpr-exports", blob, "p.json", "application/json", "v1:t");
        ReceivedFragment b = new("p", "staged", "gdpr-exports", blob, "p.json", "application/json", "v1:t");

        a.ShouldBe(b);
    }

    [Theory]
    [InlineData("staged")]
    [InlineData("passthrough")]
    [InlineData("empty")]
    public void ReceivedFragment_Accepts_KnownFragmentKinds(string fragmentKind)
    {
        ReceivedFragment sut = new("p", fragmentKind, "gdpr-exports", BlobReference.Create("ref"), "p.json", "application/json", "v1:t");

        sut.FragmentKind.ShouldBe(fragmentKind);
    }
}
