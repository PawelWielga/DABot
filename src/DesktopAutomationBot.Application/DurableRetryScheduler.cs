namespace DesktopAutomationBot.Application;

public sealed class DurableRetryScheduler : IDurableRetryScheduler
{
    private readonly IRetryRunStore _retryRunStore;
    private readonly IDurableRunResumeService _resumeService;
    private readonly TimeProvider _timeProvider;

    public DurableRetryScheduler(
        IRetryRunStore retryRunStore,
        IDurableRunResumeService resumeService,
        TimeProvider timeProvider)
    {
        _retryRunStore = retryRunStore;
        _resumeService = resumeService;
        _timeProvider = timeProvider;
    }

    public async Task<IReadOnlyList<DurableScenarioExecutionResult>> RunDueRetriesAsync(
        int maxRuns = 100,
        CancellationToken cancellationToken = default)
    {
        if (maxRuns <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxRuns),
                maxRuns,
                "Maximum run count must be greater than zero.");
        }

        var dueAt = _timeProvider.GetUtcNow();
        var runIds = await _retryRunStore.LoadDueRetryRunIdsAsync(
            dueAt,
            maxRuns,
            cancellationToken);

        var results = new List<DurableScenarioExecutionResult>(runIds.Count);

        foreach (var runId in runIds)
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(
                await _resumeService.ResumeAsync(
                    runId,
                    cancellationToken));
        }

        return results;
    }
}
