using FluentValidation;
using Granit.Metering.Endpoints.Validators;
using Granit.Timing;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Metering.Endpoints.Tests.Validators;

/// <summary>
/// Regression guard for the DI failure caused by an internal nested validator
/// whose constructor took a non-injectable <see cref="TimeSpan"/> parameter.
/// </summary>
/// <remarks>
/// <para>
/// <c>GranitValidationModule</c> calls <c>AddValidatorsFromAssembly(includeInternalTypes: true)</c>
/// over every loaded module assembly. The previous implementation defined
/// <c>MeterEventRequestValidator</c> as an <c>internal sealed class : AbstractValidator&lt;MeterEventRequest&gt;</c>
/// with a <c>(IClock clock, TimeSpan maxAge)</c> constructor, used only via
/// <c>RuleForEach(...).SetValidator(new MeterEventRequestValidator(...))</c> from inside the two
/// top-level validators. The DI auto-discovery picked it up and tried to instantiate it through
/// the container; building the <c>ServiceProvider</c> with <c>ValidateOnBuild</c> threw because
/// <see cref="TimeSpan"/> isn't a registered service. <c>SIGABRT (134)</c> at host startup.
/// </para>
/// <para>
/// The fix moved the per-event rules into a <c>static class MeterEventRules</c> with an
/// <c>Apply(InlineValidator&lt;MeterEventRequest&gt;, IClock, TimeSpan)</c> method, and the two
/// top-level validators now call <c>RuleForEach(...).ChildRules(events =&gt; MeterEventRules.Apply(events, clock, maxAge))</c>.
/// Static classes carry no <c>IValidator</c> implementation, so the assembly scanner skips them.
/// </para>
/// </remarks>
public sealed class MeteringEndpointsValidatorRegistrationTests
{
    [Fact]
    public void AddValidatorsFromAssembly_WithInternalTypes_ShouldNotPickUpHelperRuleClass()
    {
        var services = new ServiceCollection();
        services.AddValidatorsFromAssembly(
            typeof(RecordUsageRequestValidator).Assembly,
            ServiceLifetime.Scoped,
            includeInternalTypes: true);

        // Real-ish clock: validator rules call `clock.Now - maxAge` at *construction*,
        // so a default(DateTimeOffset) stub would underflow DateTime arithmetic.
        IClock clock = Substitute.For<IClock>();
        clock.Now.Returns(DateTimeOffset.UtcNow);
        services.AddSingleton(clock);

        // ValidateOnBuild surfaces "no service for type 'System.TimeSpan'" if any
        // discovered validator's ctor expects a non-DI primitive.
        ServiceProvider provider = services.BuildServiceProvider(
            new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true });

        // Both top-level validators must still resolve normally.
        using IServiceScope scope = provider.CreateScope();
        scope.ServiceProvider.GetRequiredService<IValidator<Dtos.RecordUsageRequest>>()
            .ShouldBeOfType<RecordUsageRequestValidator>();
        scope.ServiceProvider.GetRequiredService<IValidator<Dtos.BackfillUsageRequest>>()
            .ShouldBeOfType<BackfillUsageRequestValidator>();
    }
}
