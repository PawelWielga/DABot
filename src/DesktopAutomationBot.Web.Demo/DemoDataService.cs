using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoDataService :
    IRunQueryService,
    IScenarioCatalogQueryService
{
    private static readonly Guid ScenarioCheckout =
        Guid.Parse("1673266b-a930-457c-b267-dda111d81da8");
    private static readonly Guid ScenarioInventory =
        Guid.Parse("d9cd394f-a734-44f9-b2d2-c417545f7bbd");
    private static readonly Guid ScenarioApproval =
        Guid.Parse("a767cc09-8cf6-44ed-aed6-22f2b713416d");

    private static readonly IReadOnlyList<RunListItem> Runs = CreateRuns();

    private static readonly IReadOnlyList<ScenarioListItem> Scenarios =
    [
        new(
            "checkout-smoke.json",
            "Checkout smoke test",
            SchemaVersion: 1,
            StepCount: 8,
            BrowserProfile: null,
            IsValid: true,
            ValidationErrors: []),
        new(
            "inventory-watch.json",
            "Inventory availability watcher",
            SchemaVersion: 1,
            StepCount: 6,
            BrowserProfile: "shop-account",
            IsValid: true,
            ValidationErrors: []),
        new(
            "approval-flow.json",
            "Approval workflow",
            SchemaVersion: 1,
            StepCount: 11,
            BrowserProfile: "backoffice",
            IsValid: true,
            ValidationErrors: []),
        new(
            "broken-example.json",
            "broken-example",
            SchemaVersion: 0,
            StepCount: 0,
            BrowserProfile: null,
            IsValid: false,
            ValidationErrors:
            [
                "Step 'open-order' requires url for OpenUrl.",
                "Step IDs must be unique within a scenario.",
            ]),
    ];

    public Task<RunDashboardSummary> GetDashboardSummaryAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            new RunDashboardSummary
            {
                TotalRuns = Runs.Count,
                RunningRuns = Runs.Count(run => run.Status == RunStatus.Running),
                WaitingRuns = Runs.Count(run => run.Status == RunStatus.Waiting),
                FailedRuns = Runs.Count(run => run.Status == RunStatus.Failed),
                CompletedRuns = Runs.Count(run => run.Status == RunStatus.Completed),
                CancelledRuns = Runs.Count(run => run.Status == RunStatus.Cancelled),
            });
    }

    public Task<IReadOnlyList<RunListItem>> ListRecentRunsAsync(
        int limit = 50,
        RunStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var result = Runs
            .Where(run => status is null || run.Status == status)
            .OrderByDescending(run => run.UpdatedAt)
            .Take(Math.Max(0, limit))
            .ToArray();

        return Task.FromResult<IReadOnlyList<RunListItem>>(result);
    }

    public Task<IReadOnlyList<ScenarioListItem>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(Scenarios);
    }

    private static IReadOnlyList<RunListItem> CreateRuns()
    {
        var now = DateTimeOffset.UtcNow;

        return
        [
            CreateRun("a1c11111-1111-4111-8111-111111111111", ScenarioCheckout, RunStatus.Completed, now.AddMinutes(-4)),
            CreateRun("b2c22222-2222-4222-8222-222222222222", ScenarioInventory, RunStatus.Running, now.AddMinutes(-2)),
            CreateRun("c3c33333-3333-4333-8333-333333333333", ScenarioApproval, RunStatus.Waiting, now.AddMinutes(-7), RunWaitReason.Human),
            CreateRun("d4c44444-4444-4444-8444-444444444444", ScenarioInventory, RunStatus.Waiting, now.AddMinutes(-12), RunWaitReason.Event),
            CreateRun("e5c55555-5555-4555-8555-555555555555", ScenarioCheckout, RunStatus.Failed, now.AddMinutes(-19)),
            CreateRun("f6c66666-6666-4666-8666-666666666666", ScenarioCheckout, RunStatus.Completed, now.AddMinutes(-32)),
            CreateRun("17c77777-7777-4777-8777-777777777777", ScenarioApproval, RunStatus.Completed, now.AddHours(-1)),
            CreateRun("28c88888-8888-4888-8888-888888888888", ScenarioInventory, RunStatus.Cancelled, now.AddHours(-2)),
            CreateRun("39c99999-9999-4999-8999-999999999999", ScenarioCheckout, RunStatus.Completed, now.AddHours(-3)),
            CreateRun("40c00000-0000-4000-8000-000000000000", ScenarioApproval, RunStatus.Failed, now.AddHours(-5)),
            CreateRun("51c11111-aaaa-4111-8111-aaaaaaaaaaaa", ScenarioInventory, RunStatus.Completed, now.AddDays(-1)),
            CreateRun("62c22222-bbbb-4222-8222-bbbbbbbbbbbb", ScenarioCheckout, RunStatus.Completed, now.AddDays(-1).AddHours(-2)),
        ];
    }

    private static RunListItem CreateRun(
        string runId,
        Guid scenarioId,
        RunStatus status,
        DateTimeOffset updatedAt,
        RunWaitReason? waitReason = null) =>
        new()
        {
            RunId = Guid.Parse(runId),
            ScenarioId = scenarioId,
            ScenarioVersionId = Guid.NewGuid(),
            Status = status,
            WaitReason = waitReason,
            CreatedAt = updatedAt.AddMinutes(-3),
            UpdatedAt = updatedAt,
        };
}
