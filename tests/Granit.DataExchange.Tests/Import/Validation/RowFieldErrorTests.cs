using Granit.DataExchange.Import.Validation;
using Shouldly;
using Xunit;

namespace Granit.DataExchange.Tests.Import.Validation;

public sealed class RowFieldErrorTests
{
    [Fact]
    public void Constructor_SetsAllProperties()
    {
        var sut = new RowFieldError(
            "Email",
            "Validation:NotEmpty",
            "The Email field is required.");

        sut.PropertyName.ShouldBe("Email");
        sut.ErrorCode.ShouldBe("Validation:NotEmpty");
        sut.ErrorMessage.ShouldBe("The Email field is required.");
    }

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
