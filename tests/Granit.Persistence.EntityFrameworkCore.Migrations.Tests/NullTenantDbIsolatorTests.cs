using Granit.Persistence.EntityFrameworkCore.Migrations.Internal;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Persistence.EntityFrameworkCore.Migrations.Tests;

public sealed class NullTenantDbIsolatorTests
{
    [Fact]
    public async Task IsolateAsync_AlwaysCompletes_WithoutTouchingContext()
    {
        NullTenantDbIsolator isolator = new();
        DbContext context = Substitute.For<DbContext>();

        Func<Task> act = () => isolator.IsolateAsync(context, Guid.NewGuid(), CancellationToken.None);

        await Should.NotThrowAsync(act);
        context.ReceivedCalls().ShouldBeEmpty();
    }
}
