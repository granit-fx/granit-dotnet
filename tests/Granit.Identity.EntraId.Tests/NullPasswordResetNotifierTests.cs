using Granit.Identity.EntraId.Internal;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Granit.Identity.EntraId.Tests;

public sealed class NullPasswordResetNotifierTests
{
    [Fact]
    public async Task NotifyAsync_CompletesWithoutThrowing()
    {
        NullPasswordResetNotifier notifier = new(NullLogger<NullPasswordResetNotifier>.Instance);

        await notifier.NotifyAsync("user-1", "temp-password", TestContext.Current.CancellationToken);
    }
}
