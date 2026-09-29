using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class CallApiStepHandler : IStepHandler
{
    private readonly IHttpAutomationClient _httpClient;
    private readonly ISecretProvider _secretProvider;

    public CallApiStepHandler(
        IHttpAutomationClient httpClient,
        ISecretProvider secretProvider)
    {
        _httpClient = httpClient;
        _secretProvider = secretProvider;
    }

    public CallApiStepHandler(IHttpAutomationClient httpClient)
        : this(httpClient, new EnvironmentFallbackSecretProvider())
    {
    }
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

    public StepType StepType => StepType.CallApi;

    public async Task<StepExecutionResult> ExecuteAsync(
        ScenarioStep step,
        ScenarioExecutionContext context,
        int index,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(step);

        var method = GetString(step, "method")?.ToUpperInvariant() ?? "GET";
        var httpMethod = method switch
        {
            "GET" => HttpMethod.Get,
            "POST" => HttpMethod.Post,
            "PUT" => HttpMethod.Put,
            _ => throw new InvalidOperationException(
                $"CallApi method '{method}' is not supported. Supported methods: GET, POST, PUT."),
        };

        var url = step.Url ?? throw new InvalidOperationException("CallApi requires url.");
        var bearerToken = await ResolveBearerTokenAsync(
            step,
            cancellationToken);
        var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs ?? (int)DefaultTimeout.TotalMilliseconds);

        var response = await _httpClient.SendAsync(
            new HttpAutomationRequest(httpMethod, url, step.Value, bearerToken, timeout),
            cancellationToken);

        if (response.StatusCode is < 200 or >= 300)
        {
            throw new HttpRequestException(
                $"CallApi returned HTTP {response.StatusCode} for {method} {url}.");
        }

        var outputValue = step.Output is null
            ? null
            : ExtractOutput(
                response.Body,
                GetString(step, "responsePath"));

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
            OutputName = step.Output,
            OutputValue = outputValue?.ToInterpolationString(),
            OutputVariableValue = outputValue,
        };
    }

    private async ValueTask<string?> ResolveBearerTokenAsync(
        ScenarioStep step,
        CancellationToken cancellationToken)
    {
        var secretName = GetString(step, "bearerTokenSecret");
        var legacyEnvironmentName = GetString(step, "bearerTokenEnv");
        var resolvedName = secretName ?? legacyEnvironmentName;

        if (string.IsNullOrWhiteSpace(resolvedName))
        {
            return null;
        }

        return await _secretProvider.GetSecretAsync(
            resolvedName,
            cancellationToken)
            ?? throw new InvalidOperationException(
                $"Secret '{resolvedName}' configured for CallApi bearer authentication is not available.");
    }

    private static string? GetString(ScenarioStep step, string name)
    {
        if (step.Parameters is null ||
            !step.Parameters.TryGetValue(name, out var value) ||
            value.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        return value.GetString();
    }

    private sealed class EnvironmentFallbackSecretProvider : ISecretProvider
    {
        public ValueTask<string?> GetSecretAsync(
            string name,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return ValueTask.FromResult(
                Environment.GetEnvironmentVariable(name));
        }
    }

    private static ScenarioVariableValue ExtractOutput(
        string body,
        string? responsePath)
    {
        if (string.IsNullOrWhiteSpace(responsePath))
        {
            try
            {
                using var parsedBody = JsonDocument.Parse(body);
                return ScenarioVariableValue.FromJsonElement(
                    parsedBody.RootElement);
            }
            catch (JsonException)
            {
                return ScenarioVariableValue.FromString(body);
            }
        }

        using var document = JsonDocument.Parse(body);
        var current = document.RootElement;
        foreach (var segment in responsePath.Split(
                     '.',
                     StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current))
            {
                throw new InvalidOperationException(
                    $"CallApi responsePath '{responsePath}' was not found in the JSON response.");
            }
        }

        return ScenarioVariableValue.FromJsonElement(current);
    }
}
