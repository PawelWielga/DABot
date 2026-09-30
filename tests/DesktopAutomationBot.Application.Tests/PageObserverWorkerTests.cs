using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class PageObserverWorkerTests
{
    [Fact]
    public async Task RunOnceAsync_VisibleCondition_EmitsOnlyOnFalseToTrueEdge()
    {
        var now = DateTimeOffset.Parse(
            "2026-09-30T11:00:00+02:00");
        var time = new MutableTimeProvider(now);
        var observer = CreateObserver(
            PageObserverConditionKind.SelectorVisible);
        var store = new InMemoryObserverStore(observer);
        var sessions = new RecordingSessionFactory
        {
            Visible = true,
        };
        var publisher = new RecordingEventPublisher();
        var worker = new PageObserverWorker(
            store,
            sessions,
            publisher,
            time);

        (await worker.RunOnceAsync()).Should().Be(1);
        publisher.Events.Should().ContainSingle();
        sessions.Requests.Should().ContainSingle();
        sessions.Requests[0].ProfileName.Should().Be("observer-profile");

        time.Advance(TimeSpan.FromSeconds(2));

        (await worker.RunOnceAsync()).Should().Be(1);
        publisher.Events.Should().ContainSingle();

        store.Snapshot.LastMatched.Should().BeTrue();
        store.Snapshot.FailureCount.Should().Be(0);
        store.Snapshot.LastError.Should().BeNull();
    }

    [Fact]
    public async Task RunOnceAsync_TextChanged_BuildsBaselineThenEmitsChangedValue()
    {
        var now = DateTimeOffset.Parse(
            "2026-09-30T11:10:00+02:00");
        var time = new MutableTimeProvider(now);
        var observer = CreateObserver(
            PageObserverConditionKind.TextChanged);
        var store = new InMemoryObserverStore(observer);
        var sessions = new RecordingSessionFactory();
        sessions.TextValues.Enqueue("draft");
        sessions.TextValues.Enqueue("published");
        var publisher = new RecordingEventPublisher();
        var worker = new PageObserverWorker(
            store,
            sessions,
            publisher,
            time);

        await worker.RunOnceAsync();
        publisher.Events.Should().BeEmpty();
        store.Snapshot.LastObservation.Should().Be("draft");

        time.Advance(TimeSpan.FromSeconds(2));
        await worker.RunOnceAsync();

        publisher.Events.Should().ContainSingle();
        publisher.Events[0].Type.Should().Be("page.changed");
        publisher.Events[0].CorrelationId.Should().Be("observer-correlation");
        publisher.Events[0].Payload
            .ToJsonElement()
            .GetProperty("observation")
            .GetString()
            .Should()
            .Be("published");
    }


    [Fact]
    public async Task RunOnceAsync_DomChanged_BuildsHashBaselineThenEmitsOnChange()
    {
        var now = DateTimeOffset.Parse(
            "2026-09-30T11:15:00+02:00");
        var time = new MutableTimeProvider(now);
        var observer = CreateObserver(
            PageObserverConditionKind.DomChanged);
        var store = new InMemoryObserverStore(observer);
        var sessions = new RecordingSessionFactory();
        sessions.HtmlValues.Enqueue("<div id=\"status\"><span>draft</span></div>");
        sessions.HtmlValues.Enqueue("<div id=\"status\"><span>published</span></div>");
        var publisher = new RecordingEventPublisher();
        var worker = new PageObserverWorker(
            store,
            sessions,
            publisher,
            time);

        await worker.RunOnceAsync();

        publisher.Events.Should().BeEmpty();
        store.Snapshot.LastObservation.Should()
            .NotBeNullOrWhiteSpace()
            .And.HaveLength(64);

        var firstHash = store.Snapshot.LastObservation;

        time.Advance(TimeSpan.FromSeconds(2));
        await worker.RunOnceAsync();

        publisher.Events.Should().ContainSingle();
        store.Snapshot.LastObservation.Should()
            .NotBe(firstHash);
        publisher.Events[0].Payload
            .ToJsonElement()
            .GetProperty("observation")
            .GetString()
            .Should()
            .Be(store.Snapshot.LastObservation);
    }

    [Fact]
    public async Task RunOnceAsync_WhenBrowserFails_PersistsErrorBackoff()
    {
        var now = DateTimeOffset.Parse(
            "2026-09-30T11:20:00+02:00");
        var time = new MutableTimeProvider(now);
        var observer = CreateObserver(
            PageObserverConditionKind.SelectorVisible) with
        {
            PollIntervalMs = 1000,
        };
        var store = new InMemoryObserverStore(observer);
        var sessions = new RecordingSessionFactory
        {
            NavigateException =
                new InvalidOperationException("navigation failed"),
        };
        var worker = new PageObserverWorker(
            store,
            sessions,
            new RecordingEventPublisher(),
            time,
            new BotOptions
            {
                ObserverWorker = new ObserverWorkerOptions
                {
                    MaxErrorBackoffMs = 60_000,
                },
            });

        await worker.RunOnceAsync();

        store.Snapshot.FailureCount.Should().Be(1);
        store.Snapshot.LastError.Should().Be("navigation failed");
        store.Snapshot.NextCheckAt.Should().Be(
            now.AddSeconds(2));
    }

    [Theory]
    [InlineData(1, 2000)]
    [InlineData(2, 4000)]
    [InlineData(10, 5000)]
    public void CalculateErrorBackoff_UsesExponentialDelayWithCap(
        int failureCount,
        int expectedMs)
    {
        PageObserverWorker.CalculateErrorBackoff(
                pollIntervalMs: 1000,
                failureCount,
                maxErrorBackoffMs: 5000)
            .Should()
            .Be(TimeSpan.FromMilliseconds(expectedMs));
    }

    private static PageObserverDefinition CreateObserver(
        PageObserverConditionKind condition) =>
        PageObserverDefinition.Validate(
            new PageObserverDefinition
            {
                ObserverId = Guid.NewGuid(),
                Name = "test observer",
                Url = "https://example.test/status",
                BrowserProfile = "observer-profile",
                Condition = condition,
                Locator = condition == PageObserverConditionKind.UrlMatches
                    ? null
                    : ScenarioLocator.FromSelector("#status"),
                ExpectedValue = condition switch
                {
                    PageObserverConditionKind.TextEquals => "ready",
                    PageObserverConditionKind.TextContains => "ready",
                    PageObserverConditionKind.UrlMatches => "example\\.test/status",
                    _ => null,
                },
                EventType = "page.changed",
                CorrelationId = "observer-correlation",
                PollIntervalMs = 1000,
            });

    private sealed class MutableTimeProvider(
        DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance(TimeSpan value) =>
            _now = _now.Add(value);
    }

    private sealed class InMemoryObserverStore(
        PageObserverDefinition definition) : IPageObserverStore
    {
        public PageObserverSnapshot Snapshot { get; private set; } =
            new()
            {
                ObserverId = definition.ObserverId,
            };

        public Task SaveAsync(
            PageObserverDefinition definition,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<IReadOnlyList<StoredPageObserver>> LoadDueAsync(
            DateTimeOffset dueAt,
            int limit = 100,
            CancellationToken cancellationToken = default)
        {
            IReadOnlyList<StoredPageObserver> result =
                Snapshot.NextCheckAt is null ||
                Snapshot.NextCheckAt <= dueAt
                    ?
                    [
                        new StoredPageObserver
                        {
                            Definition = definition,
                            Snapshot = Snapshot,
                        },
                    ]
                    : [];

            return Task.FromResult(result);
        }

        public Task SaveSnapshotAsync(
            PageObserverSnapshot snapshot,
            CancellationToken cancellationToken = default)
        {
            Snapshot = snapshot;
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingEventPublisher : IEventPublisher
    {
        public List<AutomationEvent> Events { get; } = [];

        public Task<EventAcceptanceResult> PublishAsync(
            AutomationEvent automationEvent,
            CancellationToken cancellationToken = default)
        {
            Events.Add(automationEvent);
            return Task.FromResult(
                new EventAcceptanceResult
                {
                    IsDuplicate = false,
                    MatchedWaitingRun = false,
                });
        }
    }

    private sealed class RecordingSessionFactory :
        IBrowserSessionFactory
    {
        public bool Visible { get; init; }

        public Exception? NavigateException { get; init; }

        public Queue<string> TextValues { get; } = new();

        public List<BrowserSessionRequest> Requests { get; } = [];

        public ValueTask<IBrowserSession> CreateAsync(
            CancellationToken cancellationToken = default) =>
            CreateAsync(
                new BrowserSessionRequest(),
                cancellationToken);

        public ValueTask<IBrowserSession> CreateAsync(
            BrowserSessionRequest request,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(request);
            return ValueTask.FromResult<IBrowserSession>(
                new RecordingSession(this));
        }

        private sealed class RecordingSession(
            RecordingSessionFactory owner) : IBrowserSession
        {
            public Task OpenAsync(
                CancellationToken cancellationToken = default) =>
                Task.CompletedTask;

            public Task NavigateAsync(
                string url,
                int? timeoutMs = null,
                CancellationToken cancellationToken = default)
            {
                if (owner.NavigateException is not null)
                {
                    throw owner.NavigateException;
                }

                return Task.CompletedTask;
            }

            public Task<bool> IsVisibleAsync(
                ScenarioLocator locator,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(owner.Visible);

            public Task<string> GetCurrentUrlAsync(
                CancellationToken cancellationToken = default) =>
                Task.FromResult("https://example.test/status");

            public Task<string> ReadHtmlAsync(
                ScenarioLocator locator,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(
                    owner.HtmlValues.Count > 0
                        ? owner.HtmlValues.Dequeue()
                        : string.Empty);

            public Task<string> ReadTextAsync(
                string selector,
                int? timeoutMs = null,
                CancellationToken cancellationToken = default) =>
                Task.FromResult(
                    owner.TextValues.Count > 0
                        ? owner.TextValues.Dequeue()
                        : string.Empty);

            public Task<string> ReadTextAsync(
                ScenarioLocator locator,
                int? timeoutMs = null,
                CancellationToken cancellationToken = default) =>
                ReadTextAsync(
                    locator.Value,
                    timeoutMs,
                    cancellationToken);

            public Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task WaitForLoadStateAsync(string loadState, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
            public Task<string> TakeScreenshotAsync(string filePath, CancellationToken cancellationToken = default) => Task.FromResult(filePath);
            public ValueTask DisposeAsync() => ValueTask.CompletedTask;
        }
    }
}
