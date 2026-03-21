using FluentValidation;
using Granit.Validation.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace Granit.Validation.Tests;

public sealed class ValidationServiceCollectionExtensionsTests
{
    [Fact]
    public void AddGranitValidation_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitValidation();

        result.ShouldBeSameAs(services);
    }

    [Fact]
    public void AddGranitValidatorsFromAssemblyContaining_RegistersValidators()
    {
        ServiceCollection services = new();

        services.AddGranitValidatorsFromAssemblyContaining<TestRequest>();

        ServiceProvider provider = services.BuildServiceProvider();
        IValidator<TestRequest>? validator = provider.GetService<IValidator<TestRequest>>();

        validator.ShouldNotBeNull();
        validator.ShouldBeOfType<TestRequestValidator>();
    }

    [Fact]
    public void AddGranitValidatorsFromAssemblyContaining_ReturnsSameServiceCollection()
    {
        ServiceCollection services = new();

        IServiceCollection result = services.AddGranitValidatorsFromAssemblyContaining<TestRequest>();

        result.ShouldBeSameAs(services);
    }

    // -------------------------------------------------------------------------
    // Test doubles (must be in this assembly to be discovered)
    // -------------------------------------------------------------------------

    public sealed record TestRequest(string Name);

    public sealed class TestRequestValidator : AbstractValidator<TestRequest>
    {
        public TestRequestValidator()
        {
            RuleFor(x => x.Name).NotEmpty();
        }
    }
}
