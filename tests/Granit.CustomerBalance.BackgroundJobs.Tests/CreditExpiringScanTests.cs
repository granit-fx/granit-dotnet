using System.Reflection;
using Granit.BackgroundJobs;
using Granit.CustomerBalance.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.CustomerBalance.BackgroundJobs.Tests;

public sealed class CreditExpiringScanTests
{
    [Fact]
    public void CreditExpiringScanJob_ShouldImplementIBackgroundJob()
    {
        typeof(IBackgroundJob).IsAssignableFrom(typeof(CreditExpiringScanJob))
            .ShouldBeTrue();
    }

    [Fact]
    public void CreditExpiringScanJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(CreditExpiringScanJob)
            .GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("customer-balance-credit-expiring-scan");
        attr.CronExpression.ShouldBe("0 7 * * *");
    }

    [Fact]
    public void CreditExpiringScanHandler_ShouldBePublicNonStatic()
    {
        Type handlerType = typeof(CreditExpiringScanHandler);

        handlerType.IsPublic.ShouldBeTrue("handler must be public for Wolverine discovery");
        handlerType.IsAbstract.ShouldBeFalse("handler must not be static for Wolverine discovery");
    }

    [Fact]
    public void CreditExpiringScanHandler_HandleAsync_ShouldExist()
    {
        MethodInfo? method = typeof(CreditExpiringScanHandler)
            .GetMethod("HandleAsync", BindingFlags.Public | BindingFlags.Static);

        method.ShouldNotBeNull("HandleAsync must exist as a public static method");
        method.ReturnType.ShouldBe(typeof(Task));
    }
}
