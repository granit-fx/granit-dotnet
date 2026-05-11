using System.Linq;
using System.Reflection;
using Granit.Browsing.Diagnostics;
using Granit.DataProtection;
using Granit.Events;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Diagnostics;

public sealed class BrowsingEventsConventionTests
{
    [Fact]
    public void BrowserUrlNavigatedEvent_Url_must_be_marked_sensitive() =>
        SensitiveProperty(typeof(BrowserUrlNavigatedEvent), nameof(BrowserUrlNavigatedEvent.Url))
            .ShouldNotBeNull();

    [Fact]
    public void BrowserPageAcquiredEvent_UserAgent_must_be_marked_sensitive() =>
        SensitiveProperty(typeof(BrowserPageAcquiredEvent), nameof(BrowserPageAcquiredEvent.UserAgent))
            .ShouldNotBeNull();

    [Fact]
    public void BrowserScriptInjectedEvent_ScriptHash_must_be_marked_sensitive() =>
        SensitiveProperty(typeof(BrowserScriptInjectedEvent), nameof(BrowserScriptInjectedEvent.ScriptHash))
            .ShouldNotBeNull();

    [Theory]
    [InlineData(typeof(BrowserPageAcquiredEvent))]
    [InlineData(typeof(BrowserUrlNavigatedEvent))]
    [InlineData(typeof(BrowserScriptInjectedEvent))]
    public void Event_should_implement_IDomainEvent(System.Type t) =>
        typeof(IDomainEvent).IsAssignableFrom(t).ShouldBeTrue();

    private static SensitiveDataAttribute? SensitiveProperty(System.Type t, string propertyName) =>
        t.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance)?
            .GetCustomAttributes<SensitiveDataAttribute>(inherit: true)
            .FirstOrDefault();
}
