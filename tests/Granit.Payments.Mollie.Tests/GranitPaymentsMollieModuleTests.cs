using System.Reflection;
using System.Text;
using Granit.Modularity;
using Granit.Payments.Contracts;
using Mollie.Api.Client.Abstract;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Payments.Mollie.Tests;

public sealed class GranitPaymentsMollieModuleTests
{
    [Fact]
    public void Module_IsSealed() =>
        typeof(GranitPaymentsMollieModule).IsSealed.ShouldBeTrue();

    [Fact]
    public void Module_InheritsFromGranitModule() =>
        typeof(GranitPaymentsMollieModule)
            .IsAssignableTo(typeof(GranitModule))
            .ShouldBeTrue();

    [Fact]
    public void Module_DependsOn_GranitPaymentsModule()
    {
        DependsOnAttribute[] attrs = typeof(GranitPaymentsMollieModule)
            .GetCustomAttributes(typeof(DependsOnAttribute), false)
            .Cast<DependsOnAttribute>()
            .ToArray();

        attrs.ShouldNotBeEmpty();
        attrs.SelectMany(a => a.DependedTypes)
            .ShouldContain(typeof(GranitPaymentsModule));
    }
}

/// <summary>Helper to instantiate internal Mollie types with NSubstitute mocks.</summary>
internal static class MollieTestHelper
{
    /// <summary>Creates an instance of an internal type, substituting all constructor parameters.</summary>
    internal static object CreateInternal(string typeName)
    {
        Type type = typeof(GranitPaymentsMollieModule).Assembly
            .GetType(typeName)!;

        ConstructorInfo ctor = type.GetConstructors(
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)[0];

        object[] args = ctor.GetParameters()
            .Select(p => Substitute.For([p.ParameterType], []))
            .ToArray();

        return ctor.Invoke(args);
    }
}

public sealed class MolliePaymentProviderTests
{
    [Fact]
    public void Name_IsMollie()
    {
        var provider = (IPaymentProvider)MollieTestHelper
            .CreateInternal("Granit.Payments.Mollie.Internal.MolliePaymentProvider");

        provider.Name.ShouldBe("mollie");
    }
}

public sealed class MollieWebhookVerifierTests
{
    [Fact]
    public void ProviderName_IsMollie()
    {
        var verifier = (IPaymentWebhookVerifier)MollieTestHelper
            .CreateInternal("Granit.Payments.Mollie.Internal.MollieWebhookVerifier");

        verifier.ProviderName.ShouldBe("mollie");
    }

    [Fact]
    public async Task VerifyAsync_RejectsEmptyBody()
    {
        var verifier = (IPaymentWebhookVerifier)MollieTestHelper
            .CreateInternal("Granit.Payments.Mollie.Internal.MollieWebhookVerifier");

        PaymentWebhookVerificationResult result = await verifier.VerifyAsync(
            Encoding.UTF8.GetBytes("garbage"),
            new Dictionary<string, string>(),
            TestContext.Current.CancellationToken);

        result.IsValid.ShouldBeFalse();
    }
}

public sealed class MollieCheckoutSessionFactoryTests
{
    [Fact]
    public void ProviderName_IsMollie()
    {
        var factory = (ICheckoutSessionFactory)MollieTestHelper
            .CreateInternal("Granit.Payments.Mollie.Internal.MollieCheckoutSessionFactory");

        factory.ProviderName.ShouldBe("mollie");
    }
}

public sealed class MolliePaymentMethodManagerTests
{
    [Fact]
    public void ProviderName_IsMollie()
    {
        var manager = (IPaymentMethodManager)MollieTestHelper
            .CreateInternal("Granit.Payments.Mollie.Internal.MolliePaymentMethodManager");

        manager.ProviderName.ShouldBe("mollie");
    }
}
