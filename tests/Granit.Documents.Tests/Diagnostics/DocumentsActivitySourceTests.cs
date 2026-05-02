using System.Diagnostics;
using Granit.Documents.Diagnostics;
using Shouldly;
using Xunit;

namespace Granit.Documents.Tests.Diagnostics;

public sealed class DocumentsActivitySourceTests : IDisposable
{
    private readonly ActivityListener _listener;

    public DocumentsActivitySourceTests()
    {
        _listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == DocumentsActivitySource.Name,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) =>
                ActivitySamplingResult.AllDataAndRecorded,
        };
        ActivitySource.AddActivityListener(_listener);
    }

    public void Dispose() => _listener.Dispose();

    [Fact]
    public void Name_IsGranitDocuments() =>
        DocumentsActivitySource.Name.ShouldBe("Granit.Documents");

    [Fact]
    public void StartActivity_DocumentUpload_ReturnsActivity()
    {
        using Activity? activity = DocumentsActivitySource.Source.StartActivity(
            DocumentsActivitySource.DocumentUpload);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("documents.upload");
    }

    [Fact]
    public void StartActivity_DocumentDownload_ReturnsActivity()
    {
        using Activity? activity = DocumentsActivitySource.Source.StartActivity(
            DocumentsActivitySource.DocumentDownload);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("documents.download");
    }

    [Fact]
    public void StartActivity_DocumentShareGrant_ReturnsActivity()
    {
        using Activity? activity = DocumentsActivitySource.Source.StartActivity(
            DocumentsActivitySource.DocumentShareGrant);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("documents.share.grant");
    }

    [Fact]
    public void StartActivity_AclResolve_ReturnsActivity()
    {
        using Activity? activity = DocumentsActivitySource.Source.StartActivity(
            DocumentsActivitySource.AclResolve);

        activity.ShouldNotBeNull();
        activity.OperationName.ShouldBe("documents.acl.resolve");
    }
}
