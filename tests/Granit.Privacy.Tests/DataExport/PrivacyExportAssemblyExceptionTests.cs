using Granit.Privacy.DataExport.Exceptions;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests.DataExport;

public sealed class PrivacyExportAssemblyExceptionTests
{
    [Fact]
    public void Constructor_WithMessage_StoresRequestIdAndMessage()
    {
        var requestId = Guid.NewGuid();

        PrivacyExportAssemblyException ex = new(requestId, "transient failure");

        ex.RequestId.ShouldBe(requestId);
        ex.Message.ShouldBe("transient failure");
        ex.InnerException.ShouldBeNull();
    }

    [Fact]
    public void Constructor_WithInnerException_PreservesInner()
    {
        var requestId = Guid.NewGuid();
        InvalidOperationException inner = new("upstream blew up");

        PrivacyExportAssemblyException ex = new(requestId, "wrapped", inner);

        ex.RequestId.ShouldBe(requestId);
        ex.Message.ShouldBe("wrapped");
        ex.InnerException.ShouldBeSameAs(inner);
    }
}
