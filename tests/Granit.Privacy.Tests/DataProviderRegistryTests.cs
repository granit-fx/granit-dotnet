using Granit.Privacy.DataExport.Internal;
using Shouldly;
using Xunit;

namespace Granit.Privacy.Tests;

public sealed class DataProviderRegistryTests
{
    private readonly DataProviderRegistry _sut = new();

    [Fact]
    public void Register_AddsProvider()
    {
        _sut.Register("patients");

        _sut.Count.ShouldBe(1);
        _sut.GetAll().ShouldContain("patients");
    }

    [Fact]
    public void Register_DuplicateName_ThrowsInvalidOperationException()
    {
        _sut.Register("patients");

        Action act = () => _sut.Register("patients");

        Should.Throw<InvalidOperationException>(act).Message.ShouldContain("already registered");
    }

    [Fact]
    public void Register_IsCaseInsensitive()
    {
        _sut.Register("Patients");

        Action act = () => _sut.Register("patients");

        Should.Throw<InvalidOperationException>(act);
    }

    [Fact]
    public void Register_NullOrWhitespace_ThrowsArgumentException()
    {
        Action actNull = () => _sut.Register((string)null!);
        Action actEmpty = () => _sut.Register("");
        Action actWhitespace = () => _sut.Register("   ");

        Should.Throw<ArgumentException>(actNull);
        Should.Throw<ArgumentException>(actEmpty);
        Should.Throw<ArgumentException>(actWhitespace);
    }

    [Fact]
    public void GetAll_ReturnsAllRegistered()
    {
        _sut.Register("patients");
        _sut.Register("billing");
        _sut.Register("appointments");

        IReadOnlyList<string> result = _sut.GetAll();

        result.Count.ShouldBe(3);
        result.ShouldContain("patients");
        result.ShouldContain("billing");
        result.ShouldContain("appointments");
    }

    [Fact]
    public void Count_ReturnsCorrectCount()
    {
        _sut.Register("a");
        _sut.Register("b");

        _sut.Count.ShouldBe(2);
    }

    [Fact]
    public void Count_Empty_ReturnsZero() =>
        _sut.Count.ShouldBe(0);
}
