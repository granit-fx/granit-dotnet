using System.Reflection;
using Granit.BackgroundJobs;
using Granit.CustomerBalance.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.BackgroundJobs.Tests;

public sealed class CreditExpirationScanTests
{
    [Fact]
    public void CreditExpirationScanJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(CreditExpirationScanJob))
            .ShouldBeTrue();
    }

    [Fact]
    public void CreditExpirationScanJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(CreditExpirationScanJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("customer-balance-credit-expiration-scan");
        attr.CronExpression.ShouldBe("0 */6 * * *");
    }

    [Fact]
    public void CreditExpirationScanHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(CreditExpirationScanHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void CreditExpirationScanHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(CreditExpirationScanHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }
}
