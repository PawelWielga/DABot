using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public interface IPageObserverStore
{
    Task SaveAsync(
        PageObserverDefinition definition,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<StoredPageObserver>> LoadDueAsync(
        DateTimeOffset dueAt,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task SaveSnapshotAsync(
        PageObserverSnapshot snapshot,
        CancellationToken cancellationToken = default);
}

public interface IPageObserverWorker
{
    Task<int> RunOnceAsync(
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task RunAsync(
        ObserverWorkerOptions options,
        CancellationToken cancellationToken = default);
}

public sealed class PageObserverWorker : IPageObserverWorker
{
    private readonly IPageObserverStore _store;
    private readonly IBrowserSessionFactory _sessionFactory;
    private readonly IEventPublisher _eventPublisher;
    private readonly TimeProvider _timeProvider;
    private readonly ObserverWorkerOptions _defaultOptions;

    public PageObserverWorker(
        IPageObserverStore store,
        IBrowserSessionFactory sessionFactory,
        IEventPublisher eventPublisher,
        TimeProvider timeProvider,
        BotOptions? options = null)
    {
        _store = store;
        _sessionFactory = sessionFactory;
        _eventPublisher = eventPublisher;
        _timeProvider = timeProvider;
        _defaultOptions = options?.ObserverWorker ?? new ObserverWorkerOptions();
    }

    public async Task RunAsync(
        ObserverWorkerOptions options,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(options);

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(
                    options.BatchSize,
                    options,
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(
                    TimeSpan.FromMilliseconds(options.PollIntervalMs),
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
        }
    }

    public Task<int> RunOnceAsync(
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        ValidateOptions(_defaultOptions);
        return RunOnceAsync(
            limit,
            _defaultOptions,
            cancellationToken);
    }

    private async Task<int> RunOnceAsync(
        int limit,
        ObserverWorkerOptions options,
        CancellationToken cancellationToken)
    {
        var dueAt = _timeProvider.GetUtcNow();
        var observers = await _store.LoadDueAsync(
            dueAt,
            limit,
            cancellationToken);

        var processed = 0;

        foreach (var stored in observers)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var definition = PageObserverDefinition.Validate(
                stored.Definition);
            var snapshot = stored.Snapshot;
            var checkedAt = _timeProvider.GetUtcNow();

            try
            {
                await using var session =
                    await _sessionFactory.CreateAsync(
                        new BrowserSessionRequest
                        {
                            ProfileName = definition.BrowserProfile,
                            Headless = true,
                        },
                        cancellationToken);

                await session.OpenAsync(cancellationToken);
                await session.NavigateAsync(
                    definition.Url,
                    cancellationToken: cancellationToken);

                var evaluation = await EvaluateAsync(
                    definition,
                    snapshot,
                    session,
                    cancellationToken);

                var shouldPublish = ShouldPublish(
                    definition.Condition,
                    snapshot,
                    evaluation);

                DateTimeOffset? lastEventAt =
                    snapshot.LastEventAt;

                if (shouldPublish)
                {
                    var payload = ScenarioVariableValue.FromJsonElement(
                        JsonSerializer.SerializeToElement(
                            new
                            {
                                observerId = definition.ObserverId,
                                observerName = definition.Name,
                                condition = definition.Condition.ToString(),
                                observation = evaluation.Observation,
                                matched = evaluation.Matched,
                                checkedAt,
                            }));

                    await _eventPublisher.PublishAsync(
                        AutomationEvent.Create(
                            Guid.NewGuid(),
                            definition.EventType,
                            definition.CorrelationId,
                            payload,
                            checkedAt),
                        cancellationToken);

                    lastEventAt = checkedAt;
                }

                await _store.SaveSnapshotAsync(
                    snapshot with
                    {
                        LastObservation = evaluation.Observation,
                        LastMatched = evaluation.Matched,
                        LastCheckedAt = checkedAt,
                        NextCheckAt = checkedAt.AddMilliseconds(
                            definition.PollIntervalMs),
                        LastEventAt = lastEventAt,
                        FailureCount = 0,
                        LastError = null,
                    },
                    cancellationToken);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                var failureCount = snapshot.FailureCount + 1;
                var backoff = CalculateErrorBackoff(
                    definition.PollIntervalMs,
                    failureCount,
                    options.MaxErrorBackoffMs);

                await _store.SaveSnapshotAsync(
                    snapshot with
                    {
                        LastCheckedAt = checkedAt,
                        NextCheckAt = checkedAt.Add(backoff),
                        FailureCount = failureCount,
                        LastError = exception.Message,
                    },
                    CancellationToken.None);
            }

            processed++;
        }

        return processed;
    }

    public static TimeSpan CalculateErrorBackoff(
        int pollIntervalMs,
        int failureCount,
        int maxErrorBackoffMs)
    {
        if (pollIntervalMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(pollIntervalMs));
        }

        if (failureCount <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(failureCount));
        }

        if (maxErrorBackoffMs <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxErrorBackoffMs));
        }

        var exponent = Math.Min(failureCount, 30);
        var multiplier = 1L << exponent;
        var delayMs = Math.Min(
            (long)maxErrorBackoffMs,
            (long)pollIntervalMs * multiplier);

        return TimeSpan.FromMilliseconds(delayMs);
    }

    private static async Task<PageObserverEvaluation> EvaluateAsync(
        PageObserverDefinition definition,
        PageObserverSnapshot snapshot,
        IBrowserSession session,
        CancellationToken cancellationToken)
    {
        switch (definition.Condition)
        {
            case PageObserverConditionKind.SelectorVisible:
            {
                var visible = await session.IsVisibleAsync(
                    definition.Locator!,
                    cancellationToken);

                return new PageObserverEvaluation(
                    visible.ToString().ToLowerInvariant(),
                    visible);
            }

            case PageObserverConditionKind.SelectorHidden:
            {
                var visible = await session.IsVisibleAsync(
                    definition.Locator!,
                    cancellationToken);

                return new PageObserverEvaluation(
                    visible.ToString().ToLowerInvariant(),
                    !visible);
            }

            case PageObserverConditionKind.TextEquals:
            {
                var text = await session.ReadTextAsync(
                    definition.Locator!,
                    cancellationToken: cancellationToken);

                return new PageObserverEvaluation(
                    text,
                    string.Equals(
                        text,
                        definition.ExpectedValue,
                        StringComparison.Ordinal));
            }

            case PageObserverConditionKind.TextContains:
            {
                var text = await session.ReadTextAsync(
                    definition.Locator!,
                    cancellationToken: cancellationToken);

                return new PageObserverEvaluation(
                    text,
                    text.Contains(
                        definition.ExpectedValue!,
                        StringComparison.Ordinal));
            }

            case PageObserverConditionKind.TextChanged:
            {
                var text = await session.ReadTextAsync(
                    definition.Locator!,
                    cancellationToken: cancellationToken);

                var changed =
                    snapshot.LastObservation is not null &&
                    !string.Equals(
                        snapshot.LastObservation,
                        text,
                        StringComparison.Ordinal);

                return new PageObserverEvaluation(
                    text,
                    changed);
            }

            case PageObserverConditionKind.DomChanged:
            {
                var html = await session.ReadHtmlAsync(
                    definition.Locator!,
                    cancellationToken);
                var hash = Convert.ToHexString(
                    SHA256.HashData(
                        Encoding.UTF8.GetBytes(html)));

                var changed =
                    snapshot.LastObservation is not null &&
                    !string.Equals(
                        snapshot.LastObservation,
                        hash,
                        StringComparison.Ordinal);

                return new PageObserverEvaluation(
                    hash,
                    changed);
            }

            case PageObserverConditionKind.UrlMatches:
            {
                var currentUrl = await session.GetCurrentUrlAsync(
                    cancellationToken);
                var matched = Regex.IsMatch(
                    currentUrl,
                    definition.ExpectedValue!,
                    RegexOptions.CultureInvariant);

                return new PageObserverEvaluation(
                    currentUrl,
                    matched);
            }

            default:
                throw new ArgumentOutOfRangeException(
                    nameof(definition.Condition),
                    definition.Condition,
                    "Unsupported observer condition.");
        }
    }

    private static bool ShouldPublish(
        PageObserverConditionKind condition,
        PageObserverSnapshot snapshot,
        PageObserverEvaluation evaluation) =>
        condition is PageObserverConditionKind.TextChanged or PageObserverConditionKind.DomChanged
            ? evaluation.Matched
            : evaluation.Matched &&
              snapshot.LastMatched != true;

    private static void ValidateOptions(
        ObserverWorkerOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.PollIntervalMs <= 0)
        {
            throw new InvalidOperationException(
                "Observer worker pollIntervalMs must be greater than zero.");
        }

        if (options.BatchSize <= 0)
        {
            throw new InvalidOperationException(
                "Observer worker batchSize must be greater than zero.");
        }

        if (options.MaxErrorBackoffMs <= 0)
        {
            throw new InvalidOperationException(
                "Observer worker maxErrorBackoffMs must be greater than zero.");
        }
    }

    private sealed record PageObserverEvaluation(
        string Observation,
        bool Matched);
}
