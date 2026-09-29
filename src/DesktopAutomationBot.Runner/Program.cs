using System.Text.Json;
using DesktopAutomationBot.Application;
using DesktopAutomationBot.Core;
using DesktopAutomationBot.Infrastructure;
using DesktopAutomationBot.Runner;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;

if (!RunnerCommandLine.TryParse(
        args,
        out var commandLine,
        out var commandLineError))
{
    Console.Error.WriteLine(commandLineError);
    Console.Error.WriteLine(RunnerCommandLine.Usage);
    return RunnerExitCodes.UsageError;
}

if (commandLine.ShowHelp)
{
    Console.WriteLine(RunnerCommandLine.Usage);
    return RunnerExitCodes.Success;
}

try
{
    var configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("config.json", optional: false, reloadOnChange: false)
        .AddJsonFile("config.local.json", optional: true, reloadOnChange: false)
        .Build();

    var fileLogging = configuration
        .GetSection("logging:file")
        .Get<FileLoggingOptions>() ?? new FileLoggingOptions();

    var services = new ServiceCollection();
    services.AddSingleton<IConfiguration>(configuration);
    services.AddLogging(builder =>
    {
        builder.AddSimpleConsole(options =>
        {
            options.SingleLine = true;
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.IncludeScopes = true;
        });

        if (fileLogging.Enabled)
        {
            builder.AddProvider(new JsonFileLoggerProvider(fileLogging.Path));
        }
    });
    services.Configure<BotOptions>(configuration.GetSection("bot"));
    services.AddSingleton(sp => sp.GetRequiredService<IOptions<BotOptions>>().Value);
    services.AddApplication();
    services.AddInfrastructure();

    await using var provider = services.BuildServiceProvider();

    if (commandLine.Command == RunnerCommand.RetryWorker)
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
            return RunnerExitCodes.Success;
        }
        finally
        {
            Console.CancelKeyPress -= cancelHandler;
        }
    }

    var options = provider.GetRequiredService<BotOptions>();
    var loader = provider.GetRequiredService<IScenarioLoader>();
    var executor = provider.GetRequiredService<IScenarioExecutor>();

    var scenarioPath = commandLine.ScenarioPath ??
        options.ScenarioPath ??
        Path.Combine(options.Storage.ScenariosDirectory, "sample-open-url.json");

    using var runCancellation = new CancellationTokenSource();
    ConsoleCancelEventHandler runCancelHandler = (_, eventArgs) =>
    {
        eventArgs.Cancel = true;
        runCancellation.Cancel();
    };

    Console.CancelKeyPress += runCancelHandler;

    try
    {
        var scenario = await loader.LoadAsync(
            scenarioPath,
            runCancellation.Token);
        var result = await executor.ExecuteAsync(
            scenario,
            runCancellation.Token);

        Console.WriteLine($"Scenario: {result.ScenarioName}");
        Console.WriteLine($"Success: {result.Success}");

        foreach (var step in result.Steps)
        {
            Console.WriteLine(
                $"[{step.Index + 1}] {step.Type} => {(step.Success ? "OK" : "Failed")}");

            if (!string.IsNullOrWhiteSpace(step.OutputName))
            {
                Console.WriteLine($"    {step.OutputName} = {step.OutputValue}");
            }

            if (!string.IsNullOrWhiteSpace(step.ArtifactPath))
            {
                Console.WriteLine($"    artifact: {step.ArtifactPath}");
            }
        }

        if (!result.Success)
        {
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
            {
                Console.Error.WriteLine(result.ErrorMessage);
            }

            return RunnerExitCodes.ExecutionFailed;
        }

        return RunnerExitCodes.Success;
    }
    finally
    {
        Console.CancelKeyPress -= runCancelHandler;
    }
}
catch (OperationCanceledException)
{
    Console.Error.WriteLine("Execution cancelled.");
    return RunnerExitCodes.Cancelled;
}
catch (ScenarioLoadException exception)
{
    Console.Error.WriteLine(exception.Message);
    foreach (var error in exception.Errors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    return RunnerExitCodes.InputError;
}
catch (ScenarioValidationException exception)
{
    Console.Error.WriteLine(exception.Message);
    foreach (var error in exception.Errors)
    {
        Console.Error.WriteLine($"- {error}");
    }

    return RunnerExitCodes.InputError;
}
catch (FileNotFoundException exception)
{
    Console.Error.WriteLine(exception.Message);
    return RunnerExitCodes.InputError;
}
catch (InvalidDataException exception)
{
    Console.Error.WriteLine(exception.Message);
    return RunnerExitCodes.InputError;
}
catch (JsonException exception)
{
    Console.Error.WriteLine(exception.Message);
    return RunnerExitCodes.InputError;
}
catch (Exception exception)
{
    Console.Error.WriteLine(exception.Message);
    return RunnerExitCodes.UnexpectedError;
}
