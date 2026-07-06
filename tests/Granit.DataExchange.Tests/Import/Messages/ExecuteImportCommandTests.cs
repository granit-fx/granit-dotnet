using Granit.DataExchange.Import.Messages;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Messages;

public sealed class ExecuteImportCommandTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var jobId = Guid.NewGuid();

        ExecuteImportCommand a = new(jobId, "def");
        ExecuteImportCommand b = new(jobId, "def");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentJobIds_AreNotEqual()
    {
        ExecuteImportCommand a = new(Guid.NewGuid(), "def");
        ExecuteImportCommand b = new(Guid.NewGuid(), "def");

        a.ShouldNotBe(b);
    }
}
