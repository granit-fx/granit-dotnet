using Granit.BackgroundJobs.Domain;
using Granit.BackgroundJobs.Internal;
using Shouldly;
using Xunit;

namespace Granit.BackgroundJobs.Tests.Internal;

public sealed class RecurringJobRegistrationCollectionTests
{
    [Fact]
    public void ToList_Empty_ReturnsEmptyList()
    {
        RecurringJobRegistrationCollection collection = new();

        IReadOnlyList<RecurringJobRegistration> result = collection.ToList();

        result.ShouldBeEmpty();
    }

    [Fact]
    public void AddRange_AddsRegistrations()
    {
        RecurringJobRegistrationCollection collection = new();
        RecurringJobRegistration reg = new("job-a", "0 * * * *", "My.App.JobA, My.App");

        collection.AddRange([reg]);

        IReadOnlyList<RecurringJobRegistration> result = collection.ToList();
        result.Count.ShouldBe(1);
        result[0].JobName.ShouldBe("job-a");
    }

    [Fact]
    public void AddRange_DuplicateJobName_SkipsDuplicate()
    {
        RecurringJobRegistrationCollection collection = new();
        RecurringJobRegistration reg1 = new("job-a", "0 * * * *", "My.App.JobA, My.App");
        RecurringJobRegistration reg2 = new("job-a", "0 8 * * *", "My.App.JobA2, My.App");

        collection.AddRange([reg1]);
        collection.AddRange([reg2]);

        IReadOnlyList<RecurringJobRegistration> result = collection.ToList();
        result.Count.ShouldBe(1);
        result[0].CronExpression.ShouldBe("0 * * * *");
    }

    [Fact]
    public void AddRange_DifferentJobNames_AddsAll()
    {
        RecurringJobRegistrationCollection collection = new();
        RecurringJobRegistration reg1 = new("job-a", "0 * * * *", "My.App.JobA, My.App");
        RecurringJobRegistration reg2 = new("job-b", "0 8 * * *", "My.App.JobB, My.App");

        collection.AddRange([reg1, reg2]);

        IReadOnlyList<RecurringJobRegistration> result = collection.ToList();
        result.Count.ShouldBe(2);
    }

    [Fact]
    public void ToList_ReturnsDefensiveCopy()
    {
        RecurringJobRegistrationCollection collection = new();
        RecurringJobRegistration reg = new("job-a", "0 * * * *", "My.App.JobA, My.App");
        collection.AddRange([reg]);

        IReadOnlyList<RecurringJobRegistration> first = collection.ToList();
        IReadOnlyList<RecurringJobRegistration> second = collection.ToList();

        first.ShouldNotBeSameAs(second);
        first.Count.ShouldBe(second.Count);
    }
}
