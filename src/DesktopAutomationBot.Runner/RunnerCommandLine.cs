namespace DesktopAutomationBot.Runner;

public enum RunnerCommand
{
    Run,
    RetryWorker,
}

public sealed record RunnerCommandLine(
    RunnerCommand Command,
    string? ScenarioPath,
    bool ShowHelp)
{
    public const string Usage =
        """
        Usage:
          DesktopAutomationBot.Runner [run] [--scenario <path>]
          DesktopAutomationBot.Runner retry-worker
          DesktopAutomationBot.Runner --help

        Options:
          --scenario, -s <path>  Run the specified scenario instead of bot.scenarioPath.
          --help, -h             Show this help text.
        """;

    public static bool TryParse(
        IReadOnlyList<string> args,
        out RunnerCommandLine options,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(args);

        options = new RunnerCommandLine(
            RunnerCommand.Run,
            ScenarioPath: null,
            ShowHelp: false);
        error = null;

        if (args.Count == 0)
        {
            return true;
        }

        if (IsHelp(args[0]))
        {
            if (args.Count != 1)
            {
                error = "Help does not accept additional arguments.";
                return false;
            }

            options = options with { ShowHelp = true };
            return true;
        }

        var command = args[0].Trim().ToLowerInvariant();

        if (command == "retry-worker")
        {
            if (args.Count == 1)
            {
                options = new RunnerCommandLine(
                    RunnerCommand.RetryWorker,
                    ScenarioPath: null,
                    ShowHelp: false);
                return true;
            }

            if (args.Count == 2 && IsHelp(args[1]))
            {
                options = new RunnerCommandLine(
                    RunnerCommand.RetryWorker,
                    ScenarioPath: null,
                    ShowHelp: true);
                return true;
            }

            error = "retry-worker does not accept scenario arguments.";
            return false;
        }

        if (command != "run")
        {
            error = $"Unknown command '{args[0]}'.";
            return false;
        }

        if (args.Count == 1)
        {
            return true;
        }

        if (args.Count == 2 && IsHelp(args[1]))
        {
            options = options with { ShowHelp = true };
            return true;
        }

        if (args.Count == 3 &&
            args[1] is "--scenario" or "-s" &&
            !string.IsNullOrWhiteSpace(args[2]))
        {
            options = options with { ScenarioPath = args[2] };
            return true;
        }

        error = "Invalid run arguments.";
        return false;
    }

    private static bool IsHelp(string value) =>
        value is "--help" or "-h" or "help";
}
