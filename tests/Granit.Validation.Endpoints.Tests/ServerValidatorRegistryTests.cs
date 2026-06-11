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
            new DelegatingServerValidator("Validation:Format:Iban", _ => true));

        IServerValidator? result = registry.GetOrNull("Validation:Format:Iban");

        result.ShouldNotBeNull();
        result.ErrorCode.ShouldBe("Validation:Format:Iban");
    }

    [Fact]
    public void GetOrNull_UnknownCode_ReturnsNull()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:Format:Iban", _ => true));

        registry.GetOrNull("Validation:Unknown").ShouldBeNull();
    }

    [Fact]
    public void GetAllErrorCodes_ReturnsAllRegisteredCodes()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:Format:Iban", _ => true),
            new DelegatingServerValidator("Validation:Format:Email", _ => true));

        IReadOnlyCollection<string> codes = registry.GetAllErrorCodes();

        codes.Count.ShouldBe(2);
        codes.ShouldContain("Validation:Format:Iban");
        codes.ShouldContain("Validation:Format:Email");
    }

    [Fact]
    public void DuplicateErrorCode_KeepsFirstRegistration()
    {
        var first = new DelegatingServerValidator("Validation:Format:Iban", _ => true);
        var second = new DelegatingServerValidator("Validation:Format:Iban", _ => false);

        ServerValidatorRegistry registry = CreateRegistry(first, second);

        IServerValidator? result = registry.GetOrNull("Validation:Format:Iban");
        result.ShouldNotBeNull();
        result.Validate("anything").ShouldBeTrue();
    }

    [Fact]
    public void EmptyContributors_CreatesEmptyRegistry()
    {
        ServerValidatorRegistry registry = CreateRegistry();

        registry.GetAllErrorCodes().ShouldBeEmpty();
        registry.GetOrNull("Validation:Format:Iban").ShouldBeNull();
    }

    [Fact]
    public void GetAll_ReturnsAllValidatorInstances()
    {
        ServerValidatorRegistry registry = CreateRegistry(
            new DelegatingServerValidator("Validation:Format:Iban", _ => true),
            new DelegatingServerValidator("Validation:InvalidSsn", _ => true, isSensitive: true));

        IReadOnlyCollection<IServerValidator> all = registry.GetAll();

        all.Count.ShouldBe(2);
        all.ShouldContain(v => v.ErrorCode == "Validation:Format:Iban");
        all.ShouldContain(v => v.ErrorCode == "Validation:InvalidSsn" && v.IsSensitive);
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
