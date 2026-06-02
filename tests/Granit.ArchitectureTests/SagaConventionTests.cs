using Granit.ArchitectureTests.Abstractions.Rules;
using Wolverine;
using Xunit;

namespace Granit.ArchitectureTests;

/// <summary>
/// Validates Wolverine saga conventions.
/// Logic lives in <c>Granit.ArchitectureTests.Abstractions</c> so downstream repos can reuse it.
/// </summary>
public sealed class SagaConventionTests
{
    [Fact]
    public void Saga_start_messages_must_expose_a_resolvable_saga_identity() =>
        SagaConventionRules.SagaStartMessagesMustHaveResolvableIdentity(
            typeof(SagaConventionTests).Assembly,
            "Granit.",
            typeof(Saga));

    [Fact]
    public void Saga_methods_must_not_use_Async_suffix_for_codegen_names() =>
        SagaConventionRules.SagaMethodsMustNotUseAsyncSuffixForCodegenNames(
            typeof(SagaConventionTests).Assembly,
            "Granit.",
            typeof(Saga));
}
