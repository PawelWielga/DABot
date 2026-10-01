using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class ScenarioTestRunServiceTests
{
    [Fact]
    public async Task RunAsync_InvalidJson_ReturnsValidationErrorsWithoutExecuting()
    {
        var executor = new RecordingScenarioExecutor();
        var service = new ScenarioTestRunService(executor);

        var result = await service.RunAsync("{ not-json }");

        result.Executed.Should().BeFalse();
        result.Success.Should().BeFalse();
        result.Errors.Should().NotBeEmpty();
        executor.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_SuspendScenario_ReturnsGuardrailWithoutExecuting()
    {
        var executor = new RecordingScenarioExecutor();
        var service = new ScenarioTestRunService(executor);

        var result = await service.RunAsync(
            """
            {
              "schemaVersion": 1,
              "name": "Durable editor test",
              "steps": [
                {
                  "id": "wait",
                  "type": "Suspend",
                  "parameters": {
                    "reason": "Human"
                  }
                }
              ]
            }
            """);

        result.Executed.Should().BeFalse();
        result.Errors.Should().ContainSingle(
            error => error.Contains("one-shot", StringComparison.OrdinalIgnoreCase));
        executor.Executions.Should().BeEmpty();
    }

    [Fact]
    public async Task RunAsync_ValidScenario_ExecutesCurrentDefinition()
    {
        var executor = new RecordingScenarioExecutor();
        var service = new ScenarioTestRunService(executor);

        var result = await service.RunAsync(
            """
            {
              "schemaVersion": 1,
              "name": "Editor test run",
              "steps": [
                {
                  "id": "open",
                  "type": "OpenUrl",
                  "url": "https://example.test"
                }
              ]
            }
            """);

        result.Executed.Should().BeTrue();
        result.Success.Should().BeTrue();
        result.Errors.Should().BeEmpty();

        executor.Executions.Should().ContainSingle();
        executor.Executions[0].Name.Should().Be("Editor test run");
        executor.Executions[0].Steps.Should().ContainSingle();
        executor.Executions[0].Steps[0].Id.Should().Be("open");
    }

    private sealed class RecordingScenarioExecutor : IScenarioExecutor
    {
        public List<ScenarioDefinition> Executions { get; } = [];

        public Task<ScenarioExecutionResult> ExecuteAsync(
            ScenarioDefinition scenario,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Executions.Add(scenario);

            return Task.FromResult(
                new ScenarioExecutionResult
                {
                    ScenarioName = scenario.Name,
                    RunId = "test-run",
                    Success = true,
                });
        }
    }
}
