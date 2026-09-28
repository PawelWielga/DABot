using System.Text.Json;
using DesktopAutomationBot.Core;

namespace DesktopAutomationBot.Application;

public sealed class CallApiStepHandler(IHttpAutomationClient httpClient) : IStepHandler
{
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
        var bearerToken = ResolveBearerToken(step);
        var timeout = TimeSpan.FromMilliseconds(step.TimeoutMs ?? (int)DefaultTimeout.TotalMilliseconds);

        var response = await httpClient.SendAsync(
            new HttpAutomationRequest(httpMethod, url, step.Value, bearerToken, timeout),
            cancellationToken);

        if (response.StatusCode is < 200 or >= 300)
        {
            throw new HttpRequestException(
                $"CallApi returned HTTP {response.StatusCode} for {method} {url}.");
        }

        return new StepExecutionResult
        {
            Index = index,
            Type = StepType,
            Success = true,
            OutputName = step.Output,
            OutputValue = step.Output is null ? null : ExtractOutput(response.Body, GetString(step, "responsePath")),
        };
    }

    private static string? ResolveBearerToken(ScenarioStep step)
    {
        var environmentVariable = GetString(step, "bearerTokenEnv");
        if (string.IsNullOrWhiteSpace(environmentVariable))
        {
            return null;
        }

        return Environment.GetEnvironmentVariable(environmentVariable)
            ?? throw new InvalidOperationException(
                $"Environment variable '{environmentVariable}' configured by bearerTokenEnv is not set.");
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

    private static string ExtractOutput(string body, string? responsePath)
    {
        if (string.IsNullOrWhiteSpace(responsePath))
        {
            return body;
        }

        using var document = JsonDocument.Parse(body);
        var current = document.RootElement;
        foreach (var segment in responsePath.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (current.ValueKind != JsonValueKind.Object ||
                !current.TryGetProperty(segment, out current))
            {
                throw new InvalidOperationException(
                    $"CallApi responsePath '{responsePath}' was not found in the JSON response.");
            }
        }

        return current.ValueKind == JsonValueKind.String
            ? current.GetString() ?? string.Empty
            : current.GetRawText();
    }
}
