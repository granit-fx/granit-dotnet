using Granit.DataExchange.Import.Validation;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Validation;

public sealed class RowFieldErrorTests
{
    [Fact]
    public void Equality_SameValues_AreEqual()
    {
        var a = new RowFieldError("Name", "ERR", "msg");
        var b = new RowFieldError("Name", "ERR", "msg");

        a.ShouldBe(b);
    }

    [Fact]
    public void Equality_DifferentValues_AreNotEqual()
    {
        var a = new RowFieldError("Name", "ERR1", "msg1");
        var b = new RowFieldError("Email", "ERR2", "msg2");

        a.ShouldNotBe(b);
    }
}
