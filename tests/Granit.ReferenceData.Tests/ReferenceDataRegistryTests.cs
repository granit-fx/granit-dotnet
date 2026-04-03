using Granit.ReferenceData.Options;
using Shouldly;
using Xunit;

namespace Granit.ReferenceData.Tests;

public sealed class ReferenceDataRegistryTests
{
    private readonly ReferenceDataRegistry _registry = new();

    [Fact]
    public void Register_and_TryGet_returns_registration()
    {
        ReferenceDataTypeRegistration registration = new("Countries", new ReferenceDataExtensionOptions());

        _registry.Register(registration);

        _registry.TryGet("Countries", out ReferenceDataTypeRegistration? result).ShouldBeTrue();
        result.ShouldBe(registration);
    }

    [Fact]
    public void TryGet_is_case_insensitive()
    {
        _registry.Register(new ReferenceDataTypeRegistration("Countries", new ReferenceDataExtensionOptions()));

        _registry.TryGet("countries", out ReferenceDataTypeRegistration? result).ShouldBeTrue();
        result!.TypeName.ShouldBe("Countries");
    }

    [Fact]
    public void TryGet_returns_false_for_unknown_type()
    {
        _registry.TryGet("Unknown", out ReferenceDataTypeRegistration? result).ShouldBeFalse();
        result.ShouldBeNull();
    }

    [Fact]
    public void Register_duplicate_throws_InvalidOperationException()
    {
        _registry.Register(new ReferenceDataTypeRegistration("Countries", new ReferenceDataExtensionOptions()));

        Should.Throw<InvalidOperationException>(
            () => _registry.Register(new ReferenceDataTypeRegistration("Countries", new ReferenceDataExtensionOptions())));
    }

    [Fact]
    public void Register_duplicate_case_insensitive_throws()
    {
        _registry.Register(new ReferenceDataTypeRegistration("Countries", new ReferenceDataExtensionOptions()));

        Should.Throw<InvalidOperationException>(
            () => _registry.Register(new ReferenceDataTypeRegistration("countries", new ReferenceDataExtensionOptions())));
    }

    [Fact]
    public void Register_null_throws_ArgumentNullException()
    {
        Should.Throw<ArgumentNullException>(() => _registry.Register(null!));
    }

    [Fact]
    public void Types_returns_all_registrations()
    {
        _registry.Register(new ReferenceDataTypeRegistration("Countries", new ReferenceDataExtensionOptions()));
        _registry.Register(new ReferenceDataTypeRegistration("Currencies", new ReferenceDataExtensionOptions()));

        _registry.Types.Count.ShouldBe(2);
    }

    [Fact]
    public void Types_returns_empty_when_no_registrations()
    {
        _registry.Types.ShouldBeEmpty();
    }
}
