using Granit.Validation.ServerValidation;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Validation.Endpoints.Tests;

public sealed class ServerValidatorRegistryTests
{
    [Fact]
    public void GetOrNull_RegisteredCode_ReturnsValidator()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidIban", _ => true));

        IServerValidator? result = registry.GetOrNull("Granit:Validation:InvalidIban");

        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe("Granit:Validation:InvalidIban");
    }

    [Fact]
    public void GetOrNull_UnknownCode_ReturnsNull()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidIban", _ => true));

        registry.GetOrNull("Granit:Validation:Unknown").ShouldBeNull();
    }

    [Fact]
    public void GetAllErrorCodes_ReturnsAllRegisteredCodes()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Granit:Validation:InvalidIban", _ => true),
            new DelegatingServerValidator("Granit:Validation:InvalidEmail", _ => true));

        IReadOnlyCollection<string> codes = registry.GetAllErrorCodes();

        codes.Count.ShouldBe(2);
        codes.ShouldContain("Granit:Validation:InvalidIban");
        codes.ShouldContain("Granit:Validation:InvalidEmail");
    }

    [Fact]
    public void DuplicateErrorCode_KeepsFirstRegistration()
    {
        var first = new DelegatingServerValidator("Granit:Validation:InvalidIban", _ => true);
        var second = new DelegatingServerValidator("Granit:Validation:InvalidIban", _ => false);

        ServerValidatorRegistry registry = CreateRegistry(first, second);

        IServerValidator? result = registry.GetOrNull("Granit:Validation:InvalidIban");
        result.ShouldNotBeNull();
        result.Validate("anything").ShouldBeTrue();
    }

    [Fact]
    public void EmptyContributors_CreatesEmptyRegistry()
    {
        ServerValidatorRegistry registry = CreateRegistry();

        registry.GetAllErrorCodes().ShouldBeEmpty();
        registry.GetOrNull("Granit:Validation:InvalidIban").ShouldBeNull();
    }

    private static ServerValidatorRegistry CreateRegistry(params IServerValidator[] validators)
    {
        var contributor = new TestContributor(validators);
        ILogger<ServerValidatorRegistry> logger = Substitute.For<ILogger<ServerValidatorRegistry>>();
        return new ServerValidatorRegistry([contributor], logger);
    }

    private sealed class TestContributor(IServerValidator[] validators) : IServerValidatorContributor
    {
        public IEnumerable<IServerValidator> GetValidators() => validators;
    }
}
