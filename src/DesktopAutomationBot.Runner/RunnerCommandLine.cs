namespace DesktopAutomationBot.Runner;

public enum RunnerCommand
{
    Run,
    RetryWorker,
    ProfileSetup,
    ProfileTest,
}

public sealed record RunnerCommandLine(
    RunnerCommand Command,
    string? ScenarioPath,
    string? ProfileName,
    string? Url,
    bool ShowHelp)
{
    public const string Usage =
        """
        Usage:
          DesktopAutomationBot.Runner [run] [--scenario <path>]
          DesktopAutomationBot.Runner retry-worker
          DesktopAutomationBot.Runner profile setup --profile <name> [--url <url>]
          DesktopAutomationBot.Runner profile test --profile <name>
          DesktopAutomationBot.Runner --help

        Options:
          --scenario, -s <path>  Run the specified scenario instead of bot.scenarioPath.
          --profile, -p <name>   Select a named persistent browser profile.
          --url <url>            Optional initial URL for interactive profile setup.
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
            ProfileName: null,
            Url: null,
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
                options = options with
                {
                    Command = RunnerCommand.RetryWorker,
                };
                return true;
            }

            if (args.Count == 2 && IsHelp(args[1]))
            {
                options = options with
                {
                    Command = RunnerCommand.RetryWorker,
                    ShowHelp = true,
                };
                return true;
            }

            error = "retry-worker does not accept additional arguments.";
            return false;
        }

        if (command == "profile")
        {
            return TryParseProfile(
                args,
                options,
                out options,
                out error);
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

    private static bool TryParseProfile(
        IReadOnlyList<string> args,
        RunnerCommandLine defaults,
        out RunnerCommandLine options,
        out string? error)
    {
        options = defaults;
        error = null;

        if (args.Count < 2)
        {
            error = "profile requires a subcommand: setup or test.";
            return false;
        }

        if (IsHelp(args[1]))
        {
            options = options with { ShowHelp = true };
            return true;
        }

        var subcommand = args[1].Trim().ToLowerInvariant();
        var command = subcommand switch
        {
            "setup" => RunnerCommand.ProfileSetup,
            "test" => RunnerCommand.ProfileTest,
            _ => (RunnerCommand?)null,
        };

        if (command is null)
        {
            error = $"Unknown profile subcommand '{args[1]}'.";
            return false;
        }

        string? profileName = null;
        string? url = null;

        for (var index = 2; index < args.Count; index++)
        {
            var argument = args[index];

            if (IsHelp(argument))
            {
                options = options with
                {
                    Command = command.Value,
                    ShowHelp = true,
                };
                return true;
            }

            if (argument is "--profile" or "-p")
            {
                if (++index >= args.Count ||
                    string.IsNullOrWhiteSpace(args[index]))
                {
                    error = "--profile requires a non-empty value.";
                    return false;
                }

                profileName = args[index];
                continue;
            }

            if (argument == "--url" &&
                command == RunnerCommand.ProfileSetup)
            {
                if (++index >= args.Count ||
                    string.IsNullOrWhiteSpace(args[index]))
                {
                    error = "--url requires a non-empty value.";
                    return false;
                }

                url = args[index];
                continue;
            }

            error = $"Unknown profile argument '{argument}'.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(profileName))
        {
            error = "profile setup/test requires --profile <name>.";
            return false;
        }

        options = options with
        {
            Command = command.Value,
            ProfileName = profileName,
            Url = url,
        };
        return true;
    }

    private static bool IsHelp(string value) =>
        value is "--help" or "-h" or "help";
}
