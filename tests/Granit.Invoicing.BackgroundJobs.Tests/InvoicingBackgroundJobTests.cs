using System.Reflection;
using Granit.BackgroundJobs;
using Granit.Invoicing.BackgroundJobs.Jobs;
using Shouldly;
using Xunit;

namespace Granit.Invoicing.BackgroundJobs.Tests;

public sealed class InvoicingBackgroundJobTests
{
    // -------------------------------------------------------------------------
    // OverdueInvoiceDetectionJob
    // -------------------------------------------------------------------------

    [Fact]
    public void OverdueInvoiceDetectionJob_ShouldImplementIBackgroundJob() =>
        typeof(IBackgroundJob).IsAssignableFrom(typeof(OverdueInvoiceDetectionJob)).ShouldBeTrue();

    [Fact]
    public void OverdueInvoiceDetectionJob_ShouldBeSealedRecord() =>
        typeof(OverdueInvoiceDetectionJob).IsSealed.ShouldBeTrue();

    [Fact]
    public void OverdueInvoiceDetectionJob_ShouldHaveRecurringJobAttribute()
    {
        RecurringJobAttribute? attr = typeof(OverdueInvoiceDetectionJob).GetCustomAttribute<RecurringJobAttribute>();

        attr.ShouldNotBeNull();
        attr.Name.ShouldBe("invoicing-overdue-detection");
        attr.CronExpression.ShouldBe("0 */4 * * *");
    }
}
