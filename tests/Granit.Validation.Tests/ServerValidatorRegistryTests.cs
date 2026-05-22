using Granit.Validation.ServerValidation;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class ServerValidatorRegistryTests
{
    [Fact]
    public void GetOrNull_RegisteredCode_ReturnsValidator()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => true));

        IServerValidator? result = registry.GetOrNull("Validation:InvalidIban");

        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe("Validation:InvalidIban");
    }

    [Fact]
    public void GetOrNull_UnknownCode_ReturnsNull()
    {
        ServerValidatorRegistry registry = CreateRegistry();

        registry.GetOrNull("Validation:Unknown").ShouldBeNull();
    }

    [Fact]
    public void GetAllErrorCodes_ReturnsAllRegisteredCodes()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => true),
            new DelegatingServerValidator("Validation:InvalidEmail", _ => true));

        IReadOnlyCollection<string> codes = registry.GetAllErrorCodes();

        codes.Count.ShouldBe(2);
        codes.ShouldContain("Validation:InvalidIban");
        codes.ShouldContain("Validation:InvalidEmail");
    }

    [Fact]
    public void Constructor_NullContributors_ThrowsArgumentNullException()
    {
        ILogger<ServerValidatorRegistry> logger = Substitute.For<ILogger<ServerValidatorRegistry>>();

        Should.Throw<ArgumentNullException>(
            () => new ServerValidatorRegistry(null!, logger));
    }

    [Fact]
    public void DuplicateErrorCode_KeepsFirstRegistration()
    {
        DelegatingServerValidator first = new("Validation:InvalidIban", _ => true);
        DelegatingServerValidator second = new("Validation:InvalidIban", _ => false);

        ServerValidatorRegistry registry = CreateRegistry(first, second);

        IServerValidator? result = registry.GetOrNull("Validation:InvalidIban");
        result.ShouldNotBeNull();
        result.Validate("anything").ShouldBeTrue();
    }

    [Fact]
    public void MultipleContributors_CombineValidators()
    {
        TestContributor contributor1 = new([
            new DelegatingServerValidator("Validation:InvalidIban", _ => true),
        ]);
        TestContributor contributor2 = new([
            new DelegatingServerValidator("Validation:InvalidEmail", _ => true),
        ]);

        ILogger<ServerValidatorRegistry> logger = Substitute.For<ILogger<ServerValidatorRegistry>>();
        ServerValidatorRegistry registry = new([contributor1, contributor2], logger);

        registry.GetAllErrorCodes().Count.ShouldBe(2);
    }

    [Fact]
    public void GetAll_ReturnsAllValidatorInstances()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:InvalidIban", _ => true),
            new DelegatingServerValidator("Validation:InvalidSsn", _ => true, isSensitive: true));

        IReadOnlyCollection<IServerValidator> all = registry.GetAll();

        all.Count.ShouldBe(2);
        all.ShouldContain(v => v.ErrorCode == "Validation:InvalidIban" && !v.IsSensitive);
        all.ShouldContain(v => v.ErrorCode == "Validation:InvalidSsn" && v.IsSensitive);
    }

    // -------------------------------------------------------------------------
    // Helpers
    // -------------------------------------------------------------------------

    private static ServerValidatorRegistry CreateRegistry(params IServerValidator[] validators)
    {
        TestContributor contributor = new(validators);
        ILogger<ServerValidatorRegistry> logger = Substitute.For<ILogger<ServerValidatorRegistry>>();
        return new ServerValidatorRegistry([contributor], logger);
    }

    private sealed class TestContributor(IServerValidator[] validators) : IServerValidatorContributor
    {
        public IEnumerable<IServerValidator> GetValidators() => validators;
    }
}
