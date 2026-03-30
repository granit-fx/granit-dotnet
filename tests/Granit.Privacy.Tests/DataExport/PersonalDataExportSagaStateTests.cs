using Granit.Privacy.DataExport;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport;

public sealed class ReceivedFragmentTests
{
    [Fact]
    public void ReceivedFragment_Constructor_SetsAllProperties()
    {
        var sut = new ReceivedFragment("billing", "blob-456", "text/csv");

        sut.ProviderName.ShouldBe("billing");
        sut.BlobReferenceId.ShouldBe("blob-456");
        sut.ContentType.ShouldBe("text/csv");
    }

    [Fact]
    public void ReceivedFragment_Equality_SameValues_AreEqual()
    {
        var a = new ReceivedFragment("p", "ref", "application/json");
        var b = new ReceivedFragment("p", "ref", "application/json");

        a.ShouldBe(b);
    }
}
