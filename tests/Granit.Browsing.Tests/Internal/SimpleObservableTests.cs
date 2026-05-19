using Granit.Browsing.Internal;
using Shouldly;
using Xunit;

namespace Granit.Browsing.Tests.Internal;

public sealed class SimpleObservableTests
{
    [Fact]
    public void Publish_should_invoke_every_subscriber()
    {
        SimpleObservable<int> obs = new();
        List<int> a = [];
        List<int> b = [];

        obs.Subscribe(new RecordingObserver<int>(a));
        obs.Subscribe(new RecordingObserver<int>(b));

        obs.Publish(1);
        obs.Publish(2);

        a.ShouldBe([1, 2]);
        b.ShouldBe([1, 2]);
    }

    [Fact]
    public void Subscriber_exception_should_not_propagate_to_other_subscribers()
    {
        SimpleObservable<int> obs = new();
        List<int> sink = [];

        obs.Subscribe(new ThrowingObserver<int>());
        obs.Subscribe(new RecordingObserver<int>(sink));

        Should.NotThrow(() => obs.Publish(42));
        sink.ShouldBe([42]);
    }

    [Fact]
    public void Dispose_should_unsubscribe()
    {
        SimpleObservable<int> obs = new();
        List<int> sink = [];

        IDisposable sub = obs.Subscribe(new RecordingObserver<int>(sink));
        obs.Publish(1);
        sub.Dispose();
        obs.Publish(2);

        sink.ShouldBe([1]);
    }

    private sealed class RecordingObserver<T>(List<T> sink) : IObserver<T>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => sink.Add(value);
    }

    private sealed class ThrowingObserver<T> : IObserver<T>
    {
        public void OnCompleted() { }
        public void OnError(Exception error) { }
        public void OnNext(T value) => throw new InvalidOperationException("subscriber boom");
    }
}
