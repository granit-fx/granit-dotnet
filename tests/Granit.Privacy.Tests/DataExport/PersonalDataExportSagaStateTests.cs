// PR-1b breaking migration: the IPrivacyDataProvider streaming contract
// invalidates the call sites below. Tests are preserved for reference and
// will be rewritten under P6.2 (#2313).
//
// To re-enable while migrating: drop the #if FALSE wrapper and update each
// PersonalDataPreparedEto/ReceivedFragment construction to the new 8-arg shape,
// then convert provider.ExportAsync(userId, ct) calls to (PrivacyExportContext, ct).

using Xunit;

namespace Granit.Privacy.Tests.DataExport;

public class PersonalDataExportSagaStateTests_PendingRewrite
{
    [Fact(Skip = "P6.1b — pending rewrite under #2313 (P6.2)")]
    public void Pending() { }
}

#if FALSE_PR1B_PENDING_REWRITE
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
        sut.BlobReferenceId.Value.ShouldBe("blob-456");
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
#endif
