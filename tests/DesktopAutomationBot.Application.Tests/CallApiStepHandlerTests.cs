using System.Text.Json;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using FluentAssertions;

namespace DesktopAutomationBot.Application.Tests;

public sealed class CallApiStepHandlerTests
{
    [Fact]
    public async Task ExecuteAsync_MapsJsonResponsePathToOutput()
    {
        var client = new FakeHttpClient(new HttpAutomationResponse(200, """{"data":{"id":42}}"""));
        var handler = new CallApiStepHandler(client);
        var step = new ScenarioStep
        {
            Type = StepType.CallApi,
            Url = "https://example.test/items",
            Output = "itemId",
            Parameters = new Dictionary<string, JsonElement>
            {
                ["method"] = JsonSerializer.SerializeToElement("POST"),
                ["responsePath"] = JsonSerializer.SerializeToElement("data.id"),
            },
            Value = """{"name":"test"}""",
        };

        var result = await handler.ExecuteAsync(
            step,
            CreateContext(),
            0,
            CancellationToken.None);

        result.OutputName.Should().Be("itemId");
        result.OutputValue.Should().Be("42");
        client.Request!.Method.Should().Be(HttpMethod.Post);
        client.Request.Body.Should().Be("""{"name":"test"}""");
    }

    [Fact]
    public async Task ExecuteAsync_WhenResponseIsNotSuccessful_Throws()
    {
        var handler = new CallApiStepHandler(
            new FakeHttpClient(new HttpAutomationResponse(503, "unavailable")));

        var action = () => handler.ExecuteAsync(
            new ScenarioStep { Type = StepType.CallApi, Url = "https://example.test" },
            CreateContext(),
            0,
            CancellationToken.None);

        await action.Should().ThrowAsync<HttpRequestException>();
    }

    private static ScenarioExecutionContext CreateContext() =>
        new(
            new ScenarioDefinition { Name = "api", Steps = [new ScenarioStep { Type = StepType.CallApi }] },
            new FakeBrowserAutomation(),
            new BotOptions());

    private sealed class FakeHttpClient(HttpAutomationResponse response) : IHttpAutomationClient
    {
        public HttpAutomationRequest? Request { get; private set; }

        public Task<HttpAutomationResponse> SendAsync(
            HttpAutomationRequest request,
            CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeBrowserAutomation : IBrowserAutomation
    {
        public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NavigateAsync(string url, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClickAsync(string selector, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FillTextAsync(string selector, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PasteTextAsync(string selector, string value, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> ReadTextAsync(string selector, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForLoadStateAsync(string state, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> ScreenshotAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(path);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
