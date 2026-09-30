using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class DurableScenarioExecutorTests : IDisposable
{
    private readonly string _tempDirectory =
        Path.Combine(Path.GetTempPath(), "dabot-application-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task ExecuteAsync_PersistsStartedBeforeHandlerAndCompletesRun()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        var runId = Guid.NewGuid();
        string? contextRunId = null;

        var handler = new RecordingStepHandler(
            StepType.OpenUrl,
            events,
            (step, context, index, cancellationToken) =>
            {
                contextRunId = context.RunId;

                return Task.FromResult(
                    new StepExecutionResult
                    {
                        Index = index,
                        Type = step.Type,
                        Success = true,
                        OutputName = "captured",
                        OutputValue = "value",
                    });
            });

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(),
                RunId = runId,
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Completed);
        result.Success.Should().BeTrue();
        result.Run.State.Status.Should().Be(RunStatus.Completed);
        result.Run.Cursor.IsCompleted.Should().BeTrue();
        result.Run.Variables["captured"].ToInterpolationString().Should().Be("value");
        contextRunId.Should().Be(runId.ToString("D"));

        events.Should().ContainInOrder(
            "run:Queued",
            "run:Running",
            "browser:open",
            "attempt:Started",
            "handler:OpenUrl",
            "attempt:Completed",
            "run:Completed",
            "browser:dispose");
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerFails_FinalizesAttemptBeforeFailingRun()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);

        var handler = new RecordingStepHandler(
            StepType.OpenUrl,
            events,
            (_, _, _, _) => throw new InvalidOperationException("boom"));

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(),
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Failed);
        result.Success.Should().BeFalse();
        result.ErrorMessage.Should().Be("boom");
        result.Run.State.Status.Should().Be(RunStatus.Failed);
        result.Run.Cursor.NextStepId.Should().Be("open");

        attemptStore.Attempts.Should().HaveCount(2);
        attemptStore.Attempts[0].Status.Should().Be(StepAttemptStatus.Started);
        attemptStore.Attempts[1].Status.Should().Be(StepAttemptStatus.Failed);

        events.Should().ContainInOrder(
            "attempt:Started",
            "handler:OpenUrl",
            "attempt:Failed",
            "run:Failed");
    }

    [Fact]
    public async Task ExecuteAsync_WhenRetryableHandlerFails_SuspendsForRetry()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);

        var handler = new RecordingStepHandler(
            StepType.OpenUrl,
            events,
            (_, _, _, _) => throw new InvalidOperationException("temporary"));

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                        RetryCount = 1,
                        RetryDelayMs = 5000,
                    }),
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Suspended);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        result.Run.Cursor.NextStepId.Should().Be("open");
        result.Run.RetryNotBefore.Should().Be(
            result.Run.UpdatedAt.AddSeconds(5));
        result.ErrorMessage.Should().Be("temporary");

        events.Should().ContainInOrder(
            "attempt:Started",
            "handler:OpenUrl",
            "attempt:Failed",
            "run:Waiting");
    }

    [Fact]
    public async Task RetryNowAsync_WhenRetryDelayHasNotElapsed_RunsImmediately()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        var callCount = 0;

        var handler = new RecordingStepHandler(
            StepType.OpenUrl,
            events,
            (step, _, index, _) =>
            {
                callCount++;

                if (callCount == 1)
                {
                    throw new InvalidOperationException("temporary");
                }

                return Task.FromResult(
                    new StepExecutionResult
                    {
                        Index = index,
                        Type = step.Type,
                        Success = true,
                    });
            });

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var suspended = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                        RetryCount = 1,
                        RetryDelayMs = 60000,
                    }),
            });

        suspended.Outcome.Should().Be(DurableExecutionOutcome.Suspended);
        suspended.Run.State.Status.Should().Be(RunStatus.Waiting);
        suspended.Run.State.WaitReason.Should().Be(RunWaitReason.Retry);
        suspended.Run.RetryNotBefore.Should().NotBeNull();

        var retried = await executor.RetryNowAsync(
            suspended.Run.RunId);

        retried.Outcome.Should().Be(DurableExecutionOutcome.Completed);
        retried.Run.State.Status.Should().Be(RunStatus.Completed);
        retried.Run.Cursor.IsCompleted.Should().BeTrue();
        callCount.Should().Be(2);
        attemptStore.Attempts
            .Where(attempt => attempt.Status == StepAttemptStatus.Completed)
            .Should()
            .ContainSingle(attempt => attempt.StepId == "open");
    }

    [Fact]
    public async Task ExecuteAsync_WhenUnsafeRetryableHandlerFails_WaitsForHuman()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);

        var handler = new RecordingStepHandler(
            StepType.Click,
            events,
            (_, _, _, _) => throw new InvalidOperationException("uncertain click"));

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(
                    new ScenarioStep
                    {
                        Id = "submit",
                        Type = StepType.Click,
                        Selector = "#submit",
                        RetryCount = 1,
                    }),
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Suspended);
        result.Run.State.Status.Should().Be(RunStatus.Waiting);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Human);
        result.Run.RetryNotBefore.Should().BeNull();
        result.Run.Cursor.NextStepId.Should().Be("submit");
        result.ErrorMessage.Should().Be("uncertain click");
    }

    [Fact]
    public async Task ExecuteAsync_WhenCancellationInterruptsHandler_MarksAttemptUnknownAndCancelsRun()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        using var cancellation = new CancellationTokenSource();

        var handler = new RecordingStepHandler(
            StepType.OpenUrl,
            events,
            (_, _, _, token) =>
            {
                cancellation.Cancel();
                throw new OperationCanceledException(token);
            });

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(),
            },
            cancellation.Token);

        result.Outcome.Should().Be(DurableExecutionOutcome.Cancelled);
        result.Run.State.Status.Should().Be(RunStatus.Cancelled);
        result.Run.Cursor.NextStepId.Should().Be("open");

        attemptStore.Attempts.Should().HaveCount(2);
        attemptStore.Attempts[1].Status.Should().Be(StepAttemptStatus.Unknown);

        events.Should().ContainInOrder(
            "attempt:Started",
            "handler:OpenUrl",
            "attempt:Unknown",
            "run:Cancelled");
    }

    [Fact]
    public async Task ExecuteAsync_WithTwoSteps_PersistsCursorAfterEachCompletedStep()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);

        var handlers = new IStepHandler[]
        {
            new RecordingStepHandler(
                StepType.OpenUrl,
                events,
                (step, _, index, _) => Task.FromResult(
                    new StepExecutionResult
                    {
                        Index = index,
                        Type = step.Type,
                        Success = true,
                    })),
            new RecordingStepHandler(
                StepType.Screenshot,
                events,
                (step, _, index, _) => Task.FromResult(
                    new StepExecutionResult
                    {
                        Index = index,
                        Type = step.Type,
                        Success = true,
                    })),
        };

        var executor = CreateExecutor(
            handlers,
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateTwoStepVersion(),
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Completed);
        result.Steps.Should().HaveCount(2);

        runStore.Runs.Should().HaveCount(4);
        runStore.Runs[2].State.Status.Should().Be(RunStatus.Running);
        runStore.Runs[2].Cursor.NextStepId.Should().Be("capture");
        runStore.Runs[3].State.Status.Should().Be(RunStatus.Completed);
        runStore.Runs[3].Cursor.IsCompleted.Should().BeTrue();

        attemptStore.Attempts
            .Where(attempt => attempt.Status == StepAttemptStatus.Completed)
            .Select(attempt => attempt.StepId)
            .Should()
            .ContainInOrder("open", "capture");
    }

    [Fact]
    public async Task ExecuteAsync_WhenHandlerIsMissing_RejectsBeforePersistingOrOpeningBrowser()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);

        var executor = CreateExecutor(
            [],
            browser,
            runStore,
            attemptStore);

        Func<Task> action = async () =>
        {
            await executor.ExecuteAsync(
                new ScenarioRunRequest
                {
                    ScenarioVersion = CreateVersion(),
                });
        };

        var exception = await action.Should()
            .ThrowAsync<ScenarioValidationException>();

        exception.Which.Errors.Should().Contain(
            "scenario.steps[0].type 'OpenUrl' has no registered handler.");
        runStore.Runs.Should().BeEmpty();
        attemptStore.Attempts.Should().BeEmpty();
        events.Should().NotContain("browser:open");
    }


    [Fact]
    public async Task ExecuteAsync_WithNestedLoopAndIf_PersistsCursorAndCompletesAllIterations()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        var handler = new RecordingStepHandler(
            StepType.Screenshot,
            events,
            (step, _, index, _) => Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = step.Type,
                    Success = true,
                }));

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Nested control flow",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "loop",
                        Type = StepType.Loop,
                        Value = "2",
                        Children =
                        [
                            new ScenarioStep
                            {
                                Id = "if",
                                Type = StepType.If,
                                Value = "true",
                                Children =
                                [
                                    new ScenarioStep
                                    {
                                        Id = "capture",
                                        Type = StepType.Screenshot,
                                    },
                                ],
                            },
                        ],
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-27T19:00:00+02:00"));

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = version,
            });

        result.ErrorMessage.Should().BeNull();
        result.Outcome.Should().Be(DurableExecutionOutcome.Completed);
        result.Run.Cursor.IsCompleted.Should().BeTrue();
        result.Steps.Should().HaveCount(2);
        attemptStore.Attempts
            .Where(attempt => attempt.Status == StepAttemptStatus.Completed)
            .Select(attempt => attempt.StepId)
            .Should()
            .Equal("capture", "capture");
    }

    [Fact]
    public async Task ExecuteAsync_WhenIfIsFalse_SkipsChildrenAndContinues()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        var handler = new RecordingStepHandler(
            StepType.Screenshot,
            events,
            (step, _, index, _) => Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = step.Type,
                    Success = true,
                }));

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Conditional skip",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "if",
                        Type = StepType.If,
                        Value = "false",
                        Children =
                        [
                            new ScenarioStep
                            {
                                Id = "skipped",
                                Type = StepType.Screenshot,
                            },
                        ],
                    },
                    new ScenarioStep
                    {
                        Id = "after",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-27T19:00:00+02:00"));

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = version,
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Completed);
        result.Steps.Should().ContainSingle();
        attemptStore.Attempts
            .Where(attempt => attempt.Status == StepAttemptStatus.Completed)
            .Select(attempt => attempt.StepId)
            .Should()
            .Equal("after");
    }


    [Fact]
    public async Task ExecuteAsync_WithSuspend_PersistsNextCursorAndManualResumeContinues()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        var handler = new RecordingStepHandler(
            StepType.Screenshot,
            events,
            (step, _, index, _) => Task.FromResult(
                new StepExecutionResult
                {
                    Index = index,
                    Type = step.Type,
                    Success = true,
                }));

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Suspend and resume",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "pause",
                        Type = StepType.Suspend,
                    },
                    new ScenarioStep
                    {
                        Id = "after",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-29T09:00:00+02:00"));

        var suspended = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = version,
            });

        suspended.Outcome.Should().Be(DurableExecutionOutcome.Suspended);
        suspended.Run.State.Status.Should().Be(RunStatus.Waiting);
        suspended.Run.State.WaitReason.Should().Be(RunWaitReason.Human);
        suspended.Run.Cursor.NextStepId.Should().Be("after");

        var resumed = await executor.ResumeManuallyAsync(
            suspended.Run.RunId);

        resumed.Outcome.Should().Be(DurableExecutionOutcome.Completed);
        resumed.Run.Cursor.IsCompleted.Should().BeTrue();
        attemptStore.Attempts
            .Where(attempt => attempt.Status == StepAttemptStatus.Completed)
            .Select(attempt => attempt.StepId)
            .Should()
            .Equal("after");
    }


    [Fact]
    public async Task ExecuteAsync_WithEventSuspend_ArmsCorrelationInEventCapableRunStore()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var executor = CreateExecutor(
            [],
            new RecordingBrowserAutomation(events),
            runStore,
            new RecordingStepAttemptStore(events));

        var version = ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Event wait",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "wait",
                        Type = StepType.Suspend,
                        Parameters = new Dictionary<string, System.Text.Json.JsonElement>
                        {
                            ["reason"] = System.Text.Json.JsonSerializer.SerializeToElement("Event"),
                            ["correlationId"] = System.Text.Json.JsonSerializer.SerializeToElement("job-42"),
                            ["eventType"] = System.Text.Json.JsonSerializer.SerializeToElement("job.finished"),
                        },
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-30T08:00:00+02:00"));

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = version,
            });

        result.Outcome.Should().Be(DurableExecutionOutcome.Suspended);
        result.Run.State.WaitReason.Should().Be(RunWaitReason.Event);
        result.Run.Cursor.IsCompleted.Should().BeTrue();
        runStore.ArmedWait.Should().NotBeNull();
        runStore.ArmedWait!.RunId.Should().Be(result.Run.RunId);
        runStore.ArmedWait.CorrelationId.Should().Be("job-42");
        runStore.ArmedWait.EventType.Should().Be("job.finished");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDirectory))
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
    }

    [Fact]
    public async Task ExecuteAsync_InterpolatesInitialVariablesAndRunIdBeforeHandler()
    {
        var events = new List<string>();
        var runStore = new RecordingRunStore(events);
        var attemptStore = new RecordingStepAttemptStore(events);
        var browser = new RecordingBrowserAutomation(events);
        var runId = Guid.NewGuid();
        string? resolvedUrl = null;

        var handler = new RecordingStepHandler(
            StepType.OpenUrl,
            events,
            (step, _, index, _) =>
            {
                resolvedUrl = step.Url;

                return Task.FromResult(
                    new StepExecutionResult
                    {
                        Index = index,
                        Type = step.Type,
                        Success = true,
                    });
            });

        var executor = CreateExecutor(
            [handler],
            browser,
            runStore,
            attemptStore);

        var result = await executor.ExecuteAsync(
            new ScenarioRunRequest
            {
                ScenarioVersion = CreateVersion(
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://{{host}}/runs/{{runId}}",
                    }),
                RunId = runId,
                Variables = new Dictionary<string, string>(
                    StringComparer.OrdinalIgnoreCase)
                {
                    ["host"] = "example.com",
                },
            });

        result.Success.Should().BeTrue();
        resolvedUrl.Should().Be(
            $"https://example.com/runs/{runId:D}");
    }

    private DurableScenarioExecutor CreateExecutor(
        IEnumerable<IStepHandler> handlers,
        IBrowserAutomation browser,
        IRunStore runStore,
        IStepAttemptStore attemptStore) =>
        new(
            CreateOptions(),
            new ScenarioValidationService(),
            handlers,
            browser,
            runStore,
            attemptStore,
            new SequenceTimeProvider(
                DateTimeOffset.Parse("2026-09-27T20:00:00+02:00")));

    private BotOptions CreateOptions() =>
        new()
        {
            Storage = new StorageOptions
            {
                ScreenshotsDirectory = Path.Combine(
                    _tempDirectory,
                    "screenshots"),
            },
        };

    private static ScenarioVersion CreateVersion(
        ScenarioStep? step = null) =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Durable test",
                Steps =
                [
                    step ??
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-27T19:00:00+02:00"));

    private static ScenarioVersion CreateTwoStepVersion() =>
        ScenarioVersion.Capture(
            Guid.NewGuid(),
            1,
            new ScenarioDefinition
            {
                Name = "Durable test",
                Steps =
                [
                    new ScenarioStep
                    {
                        Id = "open",
                        Type = StepType.OpenUrl,
                        Url = "https://example.com",
                    },
                    new ScenarioStep
                    {
                        Id = "capture",
                        Type = StepType.Screenshot,
                    },
                ],
            },
            DateTimeOffset.Parse("2026-09-27T19:00:00+02:00"));

    private sealed class SequenceTimeProvider : TimeProvider
    {
        private DateTimeOffset _next;

        public SequenceTimeProvider(DateTimeOffset start)
        {
            _next = start;
        }

        public override DateTimeOffset GetUtcNow()
        {
            var current = _next;
            _next = _next.AddSeconds(1);
            return current;
        }
    }

    private sealed class RecordingRunStore : IRunStore, IEventInboxStore
    {
        private readonly List<string> _events;
        private readonly Dictionary<Guid, ScenarioVersion> _versions = [];

        public RecordingRunStore(List<string> events)
        {
            _events = events;
        }

        public List<AutomationRun> Runs { get; } = [];

        public EventWaitRegistration? ArmedWait { get; private set; }

        public Task SaveAsync(
            AutomationRun run,
            ScenarioVersion scenarioVersion,
            CancellationToken cancellationToken = default)
        {
            Runs.Add(run);
            _versions[run.RunId] = scenarioVersion;
            _events.Add($"run:{run.State.Status}");
            return Task.CompletedTask;
        }

        public Task<StoredAutomationRun?> LoadAsync(
            Guid runId,
            CancellationToken cancellationToken = default)
        {
            var run = Runs.LastOrDefault(candidate => candidate.RunId == runId);
            if (run is null)
            {
                return Task.FromResult<StoredAutomationRun?>(null);
            }

            return Task.FromResult<StoredAutomationRun?>(
                new StoredAutomationRun(
                    run,
                    _versions[runId]));
        }

        public Task ArmEventWaitAsync(
            AutomationRun run,
            ScenarioVersion scenarioVersion,
            EventWaitRegistration wait,
            CancellationToken cancellationToken = default)
        {
            Runs.Add(run);
            _versions[run.RunId] = scenarioVersion;
            ArmedWait = wait;
            _events.Add("event-wait:armed");
            return Task.CompletedTask;
        }

        public Task<EventAcceptanceResult> AcceptAsync(
            AutomationEvent automationEvent,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<IReadOnlyList<ResumeWorkItem>> LoadPendingResumeWorkItemsAsync(
            DateTimeOffset dueAt,
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResumeWorkItem>>([]);

        public Task<IReadOnlyList<ResumeWorkItem>> LoadDeadLetterResumeWorkItemsAsync(
            int limit = 100,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<ResumeWorkItem>>([]);

        public Task MarkResumeWorkItemCompletedAsync(
            Guid workItemId,
            DateTimeOffset finishedAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ScheduleResumeWorkItemRetryAsync(
            Guid workItemId,
            string errorMessage,
            DateTimeOffset nextAttemptAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task DeadLetterResumeWorkItemAsync(
            Guid workItemId,
            string errorMessage,
            DateTimeOffset finishedAt,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class RecordingStepAttemptStore : IStepAttemptStore
    {
        private readonly List<string> _events;

        public RecordingStepAttemptStore(List<string> events)
        {
            _events = events;
        }

        public List<StepAttempt> Attempts { get; } = [];

        public Task SaveStepAttemptAsync(
            StepAttempt attempt,
            CancellationToken cancellationToken = default)
        {
            Attempts.Add(attempt);
            _events.Add($"attempt:{attempt.Status}");
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StepAttempt>> LoadStepAttemptsAsync(
            Guid runId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StepAttempt>>(
                Attempts.Where(attempt => attempt.RunId == runId).ToArray());

        public Task<IReadOnlyList<StepAttempt>> MarkStartedAttemptsUnknownAsync(
            Guid runId,
            DateTimeOffset detectedAt,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<StepAttempt>>([]);
    }

    private sealed class RecordingStepHandler : IStepHandler
    {
        private readonly List<string> _events;
        private readonly Func<
            ScenarioStep,
            ScenarioExecutionContext,
            int,
            CancellationToken,
            Task<StepExecutionResult>> _execute;

        public RecordingStepHandler(
            StepType stepType,
            List<string> events,
            Func<
                ScenarioStep,
                ScenarioExecutionContext,
                int,
                CancellationToken,
                Task<StepExecutionResult>> execute)
        {
            StepType = stepType;
            _events = events;
            _execute = execute;
        }

        public StepType StepType { get; }

        public Task<StepExecutionResult> ExecuteAsync(
            ScenarioStep step,
            ScenarioExecutionContext context,
            int index,
            CancellationToken cancellationToken)
        {
            _events.Add($"handler:{StepType}");

            return _execute(
                step,
                context,
                index,
                cancellationToken);
        }
    }

    private sealed class RecordingBrowserAutomation : IBrowserAutomation
    {
        private readonly List<string> _events;

        public RecordingBrowserAutomation(List<string> events)
        {
            _events = events;
        }

        public Task OpenAsync(CancellationToken cancellationToken = default)
        {
            _events.Add("browser:open");
            return Task.CompletedTask;
        }

        public Task NavigateAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task ClickAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task FillTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task PasteTextAsync(
            string selector,
            string value,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> ReadTextAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(string.Empty);

        public Task WaitForSelectorAsync(
            string selector,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForTextAsync(
            string text,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForUrlAsync(
            string url,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task WaitForLoadStateAsync(
            string loadState,
            int? timeoutMs = null,
            CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        public Task<string> TakeScreenshotAsync(
            string filePath,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(filePath);

        public ValueTask DisposeAsync()
        {
            _events.Add("browser:dispose");
            return ValueTask.CompletedTask;
        }
    }
}
