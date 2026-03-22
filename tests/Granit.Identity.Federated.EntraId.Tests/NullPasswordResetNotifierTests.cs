using Granit.Identity.Federated.EntraId.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using Xunit;

namespace Granit.Identity.Federated.EntraId.Tests;

public sealed class NullPasswordResetNotifierTests
{
    [Fact]
    public async Task NotifyAsync_CompletesWithoutThrowing()
    {
        NullPasswordResetNotifier notifier = new(NullLogger<NullPasswordResetNotifier>.Instance);

        await Should.NotThrowAsync(() =>
            notifier.NotifyAsync("user-1", "temp-password", TestContext.Current.CancellationToken));
    }
}
