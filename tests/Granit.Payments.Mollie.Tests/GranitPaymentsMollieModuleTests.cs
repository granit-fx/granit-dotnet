using Granit.Modularity;
using Granit.Payments.Contracts;
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

public sealed class MolliePaymentProviderTests
{
    private readonly IPaymentProvider _provider;

    public MolliePaymentProviderTests()
    {
        Type? type = typeof(GranitPaymentsMollieModule).Assembly
            .GetType("Granit.Payments.Mollie.Internal.MolliePaymentProvider");

        type.ShouldNotBeNull();
        _provider = (Activator.CreateInstance(type) as IPaymentProvider)!;
    }

    [Fact]
    public void Name_IsMollie() => _provider.Name.ShouldBe("mollie");
}

public sealed class MollieWebhookVerifierTests
{
    private readonly IPaymentWebhookVerifier _verifier;

    public MollieWebhookVerifierTests()
    {
        Type? type = typeof(GranitPaymentsMollieModule).Assembly
            .GetType("Granit.Payments.Mollie.Internal.MollieWebhookVerifier");

        type.ShouldNotBeNull();
        _verifier = (Activator.CreateInstance(type) as IPaymentWebhookVerifier)!;
    }

    [Fact]
    public void ProviderName_IsMollie() => _verifier.ProviderName.ShouldBe("mollie");
}

public sealed class MollieCheckoutSessionFactoryTests
{
    private readonly ICheckoutSessionFactory _factory;

    public MollieCheckoutSessionFactoryTests()
    {
        Type? type = typeof(GranitPaymentsMollieModule).Assembly
            .GetType("Granit.Payments.Mollie.Internal.MollieCheckoutSessionFactory");

        type.ShouldNotBeNull();
        _factory = (Activator.CreateInstance(type) as ICheckoutSessionFactory)!;
    }

    [Fact]
    public void ProviderName_IsMollie() => _factory.ProviderName.ShouldBe("mollie");
}

public sealed class MolliePaymentMethodManagerTests
{
    private readonly IPaymentMethodManager _manager;

    public MolliePaymentMethodManagerTests()
    {
        Type? type = typeof(GranitPaymentsMollieModule).Assembly
            .GetType("Granit.Payments.Mollie.Internal.MolliePaymentMethodManager");

        type.ShouldNotBeNull();
        _manager = (Activator.CreateInstance(type) as IPaymentMethodManager)!;
    }

    [Fact]
    public void ProviderName_IsMollie() => _manager.ProviderName.ShouldBe("mollie");
}
