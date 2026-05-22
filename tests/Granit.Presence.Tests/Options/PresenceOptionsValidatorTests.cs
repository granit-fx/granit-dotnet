using Granit.Presence.Options;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace Granit.Presence.Tests.Options;

public sealed class PresenceOptionsValidatorTests
{
    private readonly PresenceOptionsValidator _validator = new();

    [Fact]
    public void Validate_passes_with_defaults()
    {
        ValidateOptionsResult result = _validator.Validate(null, new PresenceOptions());

        result.Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_offline_threshold_negative()
    {
        PresenceOptions options = new() { OfflineThreshold = TimeSpan.FromSeconds(-1) };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_heartbeat_ttl_smaller_than_offline_threshold()
    {
        PresenceOptions options = new()
        {
            OfflineThreshold = TimeSpan.FromSeconds(120),
            HeartbeatCacheTtl = TimeSpan.FromSeconds(60),
        };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }

    [Fact]
    public void Validate_fails_when_batch_size_too_large()
    {
        PresenceOptions options = new() { MaxBatchSize = 5000 };

        ValidateOptionsResult result = _validator.Validate(null, options);

        result.Failed.ShouldBeTrue();
    }
}
