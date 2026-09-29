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

        var result = await handler.ExecuteAsync(step, CreateContext(), 0, CancellationToken.None);

        result.OutputName.Should().Be("itemId");
        result.OutputValue.Should().Be("42");
        result.OutputVariableValue.Should().NotBeNull();
        result.OutputVariableValue!.Kind.Should().Be(JsonValueKind.Number);
        client.Request!.Method.Should().Be(HttpMethod.Post);
        client.Request.Body.Should().Be("""{"name":"test"}""");
    }


    [Fact]
    public async Task ExecuteAsync_WithNamedSecret_ResolvesBearerTokenWithoutUsingRunVariables()
    {
        var client = new FakeHttpClient(
            new HttpAutomationResponse(200, "{}"));
        var secrets = new FakeSecretProvider(
            "api-token",
            "super-secret");
        var handler = new CallApiStepHandler(
            client,
            secrets);

        var step = new ScenarioStep
        {
            Type = StepType.CallApi,
            Url = "https://example.test/private",
            Parameters = new Dictionary<string, JsonElement>
            {
                ["bearerTokenSecret"] =
                    JsonSerializer.SerializeToElement("api-token"),
            },
        };

        await handler.ExecuteAsync(
            step,
            CreateContext(),
            0,
            CancellationToken.None);

        client.Request!.BearerToken.Should().Be("super-secret");
        secrets.RequestedName.Should().Be("api-token");
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


    private sealed class FakeSecretProvider(
        string expectedName,
        string value) : ISecretProvider
    {
        public string? RequestedName { get; private set; }

        public ValueTask<string?> GetSecretAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            RequestedName = name;
            name.Should().Be(expectedName);
            return ValueTask.FromResult<string?>(value);
        }
    }

    private sealed class FakeHttpClient(HttpAutomationResponse response) : IHttpAutomationClient
    {
        public HttpAutomationRequest? Request { get; private set; }

        public Task<HttpAutomationResponse> SendAsync(HttpAutomationRequest request, CancellationToken cancellationToken)
        {
            Request = request;
            return Task.FromResult(response);
        }
    }

    private sealed class FakeBrowserAutomation : IBrowserAutomation
    {
        public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task NavigateAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ClickAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task FillTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task PasteTextAsync(string selector, string value, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> ReadTextAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.FromResult(string.Empty);
        public Task WaitForSelectorAsync(string selector, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForTextAsync(string text, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForUrlAsync(string url, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task WaitForLoadStateAsync(string state, int? timeoutMs = null, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task<string> TakeScreenshotAsync(string path, CancellationToken cancellationToken = default) => Task.FromResult(path);
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
