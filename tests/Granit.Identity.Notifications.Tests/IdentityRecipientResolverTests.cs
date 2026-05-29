using Granit.Identity.Domain;
using Granit.Identity.Notifications.Internal;
using Granit.Identity.Notifications.Options;
using Granit.Notifications;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;
using Xunit;

namespace Granit.Identity.Notifications.Tests;

public sealed class IdentityRecipientResolverTests
{
    private const string UserId = "11111111-1111-1111-1111-111111111111";

    private readonly IIdentityUserReader _reader = Substitute.For<IIdentityUserReader>();

    private IdentityRecipientResolver CreateResolver(IdentityRecipientResolverOptions? options = null) =>
        new(
            _reader,
            Microsoft.Extensions.Options.Options.Create(options ?? new IdentityRecipientResolverOptions()),
            NullLogger<IdentityRecipientResolver>.Instance);

    [Fact]
    public async Task ResolveAsync_returns_null_when_user_not_found()
    {
        _reader.GetUserAsync(UserId, Arg.Any<CancellationToken>()).Returns((IIdentityUser?)null);

        RecipientInfo? recipient = await CreateResolver().ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_throws_when_userId_is_empty()
    {
        await Should.ThrowAsync<ArgumentException>(
            () => CreateResolver().ResolveAsync(string.Empty, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ResolveAsync_maps_email_and_builds_display_name_from_names()
    {
        SetUser(new FakeIdentityUser
        {
            UserId = UserId,
            Email = "jane.doe@example.com",
            FirstName = "Jane",
            LastName = "Doe",
        });

        RecipientInfo? recipient = await CreateResolver().ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient.ShouldNotBeNull();
        recipient.UserId.ShouldBe(UserId);
        recipient.Email.ShouldBe("jane.doe@example.com");
        recipient.DisplayName.ShouldBe("Jane Doe");
    }

    [Fact]
    public async Task ResolveAsync_falls_back_to_username_when_names_missing()
    {
        SetUser(new FakeIdentityUser
        {
            UserId = UserId,
            Email = "svc@example.com",
            Username = "service-account",
        });

        RecipientInfo? recipient = await CreateResolver().ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient!.DisplayName.ShouldBe("service-account");
    }

    [Fact]
    public async Task ResolveAsync_resolves_phone_and_culture_from_default_metadata_keys()
    {
        SetUser(new FakeIdentityUser
        {
            UserId = UserId,
            Email = "kc@example.com",
            Metadata = new Dictionary<string, string>
            {
                ["phoneNumber"] = "+3225550100",
                ["locale"] = "fr",
            },
        });

        RecipientInfo? recipient = await CreateResolver().ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient!.PhoneNumber.ShouldBe("+3225550100");
        recipient.PreferredCulture.ShouldBe("fr");
    }

    [Fact]
    public async Task ResolveAsync_probes_secondary_metadata_keys_in_order()
    {
        SetUser(new FakeIdentityUser
        {
            UserId = UserId,
            Email = "cognito@example.com",
            Metadata = new Dictionary<string, string>
            {
                ["phone_number"] = "+12025550123",
                ["preferredLanguage"] = "de",
            },
        });

        RecipientInfo? recipient = await CreateResolver().ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient!.PhoneNumber.ShouldBe("+12025550123");
        recipient.PreferredCulture.ShouldBe("de");
    }

    [Fact]
    public async Task ResolveAsync_honors_custom_metadata_keys_from_options()
    {
        SetUser(new FakeIdentityUser
        {
            UserId = UserId,
            Email = "custom@example.com",
            Metadata = new Dictionary<string, string>
            {
                ["custom:mobile"] = "+447700900123",
            },
        });

        IdentityRecipientResolverOptions options = new()
        {
            PhoneNumberMetadataKeys = ["custom:mobile"],
        };

        RecipientInfo? recipient = await CreateResolver(options).ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient!.PhoneNumber.ShouldBe("+447700900123");
    }

    [Fact]
    public async Task ResolveAsync_applies_default_culture_when_none_resolved()
    {
        SetUser(new FakeIdentityUser { UserId = UserId, Email = "nolocale@example.com" });

        IdentityRecipientResolverOptions options = new() { DefaultCulture = "en" };

        RecipientInfo? recipient = await CreateResolver(options).ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient!.PreferredCulture.ShouldBe("en");
        recipient.PhoneNumber.ShouldBeNull();
    }

    [Fact]
    public async Task ResolveAsync_prefers_canonical_user_first_class_fields_over_metadata()
    {
        var user = User.Create(
            id: Guid.Parse(UserId),
            email: "canonical@example.com",
            displayName: "Canon Ical",
            firstName: "Canon",
            lastName: "Ical");
        user.UpdateProfile(
            displayName: "Canon Ical",
            email: "canonical@example.com",
            firstName: "Canon",
            lastName: "Ical",
            phoneNumber: "+3225559999",
            preferredLocale: "nl",
            timezone: "Europe/Brussels");

        SetUser(user);

        RecipientInfo? recipient = await CreateResolver().ResolveAsync(UserId, TestContext.Current.CancellationToken);

        recipient!.Email.ShouldBe("canonical@example.com");
        recipient.DisplayName.ShouldBe("Canon Ical");
        recipient.PhoneNumber.ShouldBe("+3225559999");
        recipient.PreferredCulture.ShouldBe("nl");
    }

    private void SetUser(IIdentityUser user) =>
        _reader.GetUserAsync(UserId, Arg.Any<CancellationToken>()).Returns(user);

    private sealed class FakeIdentityUser : IIdentityUser
    {
        public required string UserId { get; init; }

        public string? Username { get; init; }

        public string? Email { get; init; }

        public string? FirstName { get; init; }

        public string? LastName { get; init; }

        public bool Enabled { get; init; } = true;

        public IReadOnlyDictionary<string, string> Metadata { get; init; } =
            new Dictionary<string, string>();
    }
}
