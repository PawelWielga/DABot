using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Web.Demo;

public sealed class DemoDataService :
    IRunQueryService,
    IScenarioCatalogQueryService,
    IScenarioManagementService
{
    private static readonly Guid ScenarioCheckout =
        Guid.Parse("1673266b-a930-457c-b267-dda111d81da8");
    private static readonly Guid ScenarioInventory =
        Guid.Parse("d9cd394f-a734-44f9-b2d2-c417545f7bbd");
    private static readonly Guid ScenarioApproval =
        Guid.Parse("a767cc09-8cf6-44ed-aed6-22f2b713416d");

    private static readonly IReadOnlyList<RunListItem> Runs = CreateRuns();

    private readonly Dictionary<string, string> _scenarioDocuments =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["checkout-smoke.json"] = """
                {
                  "schemaVersion": 1,
                  "name": "Checkout smoke test",
                  "steps": [
                    {
                      "id": "open",
                      "type": "OpenUrl",
                      "url": "https://example.test/checkout"
                    }
                  ]
                }
                """,
            ["inventory-watch.json"] = """
                {
                  "schemaVersion": 1,
                  "name": "Inventory availability watcher",
                  "browserProfile": "shop-account",
                  "steps": [
                    {
                      "id": "open",
                      "type": "OpenUrl",
                      "url": "https://example.test/products"
                    },
                    {
                      "id": "wait",
                      "type": "WaitFor",
                      "selector": "[data-stock='available']"
                    }
                  ]
                }
                """,
            ["approval-flow.json"] = """
                {
                  "schemaVersion": 1,
                  "name": "Approval workflow",
                  "browserProfile": "backoffice",
                  "steps": [
                    {
                      "id": "open",
                      "type": "OpenUrl",
                      "url": "https://example.test/approvals"
                    },
                    {
                      "id": "suspend",
                      "type": "Suspend",
                      "parameters": {
                        "reason": "Event",
                        "correlationId": "approval-123",
                        "eventType": "approval.completed"
                      }
                    }
                  ]
                }
                """,
            ["broken-example.json"] = """
                {
                  "schemaVersion": 1,
                  "name": "Broken scenario",
                  "steps": [
                    {
                      "id": "open",
                      "type": "OpenUrl"
                    }
                  ]
                }
                """,
        };

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

        var scenarios = _scenarioDocuments
            .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(pair => ToListItem(pair.Key, pair.Value))
            .ToArray();

        return Task.FromResult<IReadOnlyList<ScenarioListItem>>(scenarios);
    }

    public Task<ScenarioDocument?> GetAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScenarioFileName.Validate(fileName);

        return Task.FromResult(
            _scenarioDocuments.TryGetValue(fileName, out var json)
                ? new ScenarioDocument(fileName, json)
                : null);
    }

    public Task<ScenarioWriteResult> SaveAsync(
        string fileName,
        string json,
        bool overwrite,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScenarioFileName.Validate(fileName);

        var validation = ScenarioJsonValidation.Validate(json);
        if (!validation.IsValid)
        {
            return Task.FromResult(
                new ScenarioWriteResult(false, validation.Errors));
        }

        if (!overwrite && _scenarioDocuments.ContainsKey(fileName))
        {
            return Task.FromResult(
                new ScenarioWriteResult(
                    false,
                    [$"Scenario file '{fileName}' already exists."]));
        }

        _scenarioDocuments[fileName] = json;
        return Task.FromResult(new ScenarioWriteResult(true, []));
    }

    public Task<bool> DeleteAsync(
        string fileName,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        ScenarioFileName.Validate(fileName);
        return Task.FromResult(_scenarioDocuments.Remove(fileName));
    }

    private static ScenarioListItem ToListItem(
        string fileName,
        string json)
    {
        var validation = ScenarioJsonValidation.Validate(json);
        if (!validation.IsValid)
        {
            return new ScenarioListItem(
                fileName,
                Path.GetFileNameWithoutExtension(fileName),
                0,
                0,
                null,
                false,
                validation.Errors);
        }

        using var document = System.Text.Json.JsonDocument.Parse(json);
        var root = document.RootElement;
        var name = root.TryGetProperty("name", out var nameElement)
            ? nameElement.GetString() ?? Path.GetFileNameWithoutExtension(fileName)
            : Path.GetFileNameWithoutExtension(fileName);
        var schemaVersion = root.TryGetProperty("schemaVersion", out var schemaElement)
            ? schemaElement.GetInt32()
            : ScenarioSchema.CurrentVersion;
        var stepCount = root.TryGetProperty("steps", out var stepsElement)
            ? stepsElement.GetArrayLength()
            : 0;
        var browserProfile = root.TryGetProperty("browserProfile", out var profileElement)
            ? profileElement.GetString()
            : null;

        return new ScenarioListItem(
            fileName,
            name,
            schemaVersion,
            stepCount,
            browserProfile,
            true,
            []);
    }

    public Task<RunDetail?> GetRunAsync(
        Guid runId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var run = Runs.SingleOrDefault(item => item.RunId == runId);
        if (run is null)
        {
            return Task.FromResult<RunDetail?>(null);
        }

        var scenarioName = run.ScenarioId == ScenarioCheckout
            ? "Checkout smoke test"
            : run.ScenarioId == ScenarioInventory
                ? "Inventory availability watcher"
                : "Approval workflow";

        IReadOnlyList<RunVariableItem> variables =
        [
            new("environment", "String", "demo"),
            new("customerId", "String", "CUST-1042"),
            new("attempt", "Number", "2"),
        ];

        IReadOnlyList<RunStepAttemptItem> attempts =
        [
            new()
            {
                AttemptId = Guid.Parse("71000000-0000-4000-8000-000000000001"),
                StepId = "open",
                StepType = StepType.OpenUrl,
                AttemptNumber = 1,
                RetrySafety = StepRetrySafety.Idempotent,
                Status = StepAttemptStatus.Completed,
                StartedAt = run.CreatedAt,
                UpdatedAt = run.CreatedAt.AddMilliseconds(720),
                FinishedAt = run.CreatedAt.AddMilliseconds(720),
            },
            new()
            {
                AttemptId = Guid.Parse("71000000-0000-4000-8000-000000000002"),
                StepId = "read-status",
                StepType = StepType.ReadText,
                AttemptNumber = 1,
                RetrySafety = StepRetrySafety.SafeToRetry,
                Status = run.Status == RunStatus.Failed
                    ? StepAttemptStatus.Failed
                    : StepAttemptStatus.Completed,
                StartedAt = run.CreatedAt.AddSeconds(1),
                UpdatedAt = run.CreatedAt.AddSeconds(2),
                FinishedAt = run.CreatedAt.AddSeconds(2),
                ErrorMessage = run.Status == RunStatus.Failed
                    ? "Demo failure: expected page state was not reached."
                    : null,
            },
        ];

        IReadOnlyList<RunEventItem> events =
            run.Status == RunStatus.Waiting &&
            run.WaitReason == RunWaitReason.Event
                ?
                [
                    new()
                    {
                        EventId = Guid.Parse("72000000-0000-4000-8000-000000000001"),
                        Type = "approval.completed",
                        CorrelationId = "approval-123",
                        OccurredAt = run.UpdatedAt.AddMinutes(-1),
                        ReceivedAt = run.UpdatedAt.AddSeconds(-40),
                        WorkItemId = Guid.Parse("73000000-0000-4000-8000-000000000001"),
                        WorkItemStatus = ResumeWorkItemStatus.Pending,
                        AttemptCount = 0,
                    },
                ]
                :
                [
                    new()
                    {
                        EventId = Guid.Parse("72000000-0000-4000-8000-000000000002"),
                        Type = "order.updated",
                        CorrelationId = "order-demo-42",
                        OccurredAt = run.CreatedAt.AddSeconds(4),
                        ReceivedAt = run.CreatedAt.AddSeconds(5),
                        WorkItemId = Guid.Parse("73000000-0000-4000-8000-000000000002"),
                        WorkItemStatus = ResumeWorkItemStatus.Completed,
                        AttemptCount = 1,
                        FinishedAt = run.CreatedAt.AddSeconds(6),
                    },
                ];

        return Task.FromResult<RunDetail?>(
            new RunDetail
            {
                RunId = run.RunId,
                ScenarioId = run.ScenarioId,
                ScenarioVersionId = run.ScenarioVersionId,
                ScenarioVersionNumber = 3,
                ScenarioName = scenarioName,
                Status = run.Status,
                WaitReason = run.WaitReason,
                RetryNotBefore = run.Status == RunStatus.Waiting &&
                                 run.WaitReason == RunWaitReason.Retry
                    ? run.UpdatedAt.AddMinutes(1)
                    : null,
                CreatedAt = run.CreatedAt,
                UpdatedAt = run.UpdatedAt,
                Variables = variables,
                StepAttempts = attempts,
                Events = events,
            });
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
