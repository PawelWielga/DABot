using DesktopAutomationBot.Application;
using DesktopAutomationBot.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("config.json", optional: false, reloadOnChange: false)
    .AddJsonFile("config.local.json", optional: true, reloadOnChange: false)
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(configuration);
services.Configure<BotOptions>(configuration.GetSection("bot"));
services.AddSingleton(sp => sp.GetRequiredService<IOptions<BotOptions>>().Value);
services.AddApplication();
services.AddInfrastructure();

await using var provider = services.BuildServiceProvider();

try
{
    var command = args.Length == 0
        ? "run"
        : args[0].Trim().ToLowerInvariant();

    if (args.Length > 1 ||
        command is not ("run" or "retry-worker"))
    {
        Console.Error.WriteLine(
            "Usage: DesktopAutomationBot.Runner [run|retry-worker]");
        return 2;
    }

    if (command == "retry-worker")
    {
        var worker = provider.GetRequiredService<IDurableRetryWorker>();
        var workerOptions = provider.GetRequiredService<BotOptions>();

        using var cancellation = new CancellationTokenSource();
        ConsoleCancelEventHandler cancelHandler = (_, eventArgs) =>
        {
            eventArgs.Cancel = true;
            cancellation.Cancel();
        };

        Console.CancelKeyPress += cancelHandler;

        try
        {
            Console.WriteLine(
                $"Retry worker started. Poll interval: {workerOptions.RetryWorker.PollIntervalMs} ms; " +
                $"batch size: {workerOptions.RetryWorker.BatchSize}. Press Ctrl+C to stop.");

            await worker.RunAsync(cancellation.Token);
            return 0;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    var options = provider.GetRequiredService<BotOptions>();
    var loader = provider.GetRequiredService<IScenarioLoader>();
    var executor = provider.GetRequiredService<IScenarioExecutor>();

    var scenarioPath = options.ScenarioPath ?? Path.Combine(options.Storage.ScenariosDirectory, "sample-open-url.json");
    var scenario = await loader.LoadAsync(scenarioPath);
    var result = await executor.ExecuteAsync(scenario);

    Console.WriteLine($"Scenario: {result.ScenarioName}");
    Console.WriteLine($"Success: {result.Success}");

    foreach (var step in result.Steps)
    {
        Console.WriteLine($"[{step.Index + 1}] {step.Type} => {(step.Success ? "OK" : "Failed")}");

        if (!string.IsNullOrWhiteSpace(step.OutputName))
        {
            Console.WriteLine($"    {step.OutputName} = {step.OutputValue}");
        }

        if (!string.IsNullOrWhiteSpace(step.ArtifactPath))
        {
            Console.WriteLine($"    artifact: {step.ArtifactPath}");
        }
    }

    if (!result.Success && !string.IsNullOrWhiteSpace(result.ErrorMessage))
    {
        Console.Error.WriteLine(result.ErrorMessage);
        return 1;
    }

    return 0;
}

catch (ScenarioLoadException exception)
{
    Console.Error.WriteLine(exception.Message);
    foreach (var error in exception.Errors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    return 1;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return 1;
}
