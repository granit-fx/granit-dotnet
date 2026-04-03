using Granit.Tax.Stripe;
using Shouldly;
using Xunit;

namespace Granit.Tax.Stripe.Tests;

public sealed class TaxStripeModuleTests
{
    [Fact]
    public void GranitTaxStripeModule_should_be_instantiable()
    {
        var module = new GranitTaxStripeModule();
        module.ShouldNotBeNull();
    }
}
