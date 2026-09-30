namespace DesktopAutomationBot.Runner;

public enum RunnerCommand
{
    Run,
    RetryWorker,
    EventWorker,
    EventPublish,
    ObserverWorker,
    ObserverImport,
    ProfileSetup,
    ProfileTest,
    Resume,
    Cancel,
}

public sealed record RunnerCommandLine(
    RunnerCommand Command,
    string? ScenarioPath,
    string? ProfileName,
    string? Url,
    Guid? RunId,
    Guid? EventId,
    string? EventType,
    string? CorrelationId,
    string? PayloadJson,
    string? ObserverPath,
    bool ShowHelp)
{
    public const string Usage =
        """
        Usage:
          DesktopAutomationBot.Runner [run] [--scenario <path>]
          DesktopAutomationBot.Runner retry-worker
          DesktopAutomationBot.Runner event-worker
          DesktopAutomationBot.Runner event publish --id <guid> --type <type> --correlation <id> [--payload <json>]
          DesktopAutomationBot.Runner observer-worker
          DesktopAutomationBot.Runner observer import --file <path>
          DesktopAutomationBot.Runner profile setup --profile <name> [--url <url>]
          DesktopAutomationBot.Runner profile test --profile <name>
          DesktopAutomationBot.Runner resume --run-id <guid>
          DesktopAutomationBot.Runner cancel --run-id <guid>
          DesktopAutomationBot.Runner --help

        Options:
          --scenario, -s <path>  Run the specified scenario instead of bot.scenarioPath.
          --profile, -p <name>   Select a named persistent browser profile.
          --url <url>            Optional initial URL for interactive profile setup.
          --run-id <guid>        Select a persisted durable run.
          --id <guid>            Stable event ID used for idempotent event delivery.
          --type <type>          Event type.
          --correlation <id>     Event correlation ID.
          --payload <json>       Optional structured JSON event payload; defaults to null.
          --file <path>          JSON definition file for observer import.
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
            RunId: null,
            EventId: null,
            EventType: null,
            CorrelationId: null,
            PayloadJson: null,
            ObserverPath: null,
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

        if (command is "retry-worker" or "event-worker" or "observer-worker")
        {
            var workerCommand = command switch
            {
                "retry-worker" => RunnerCommand.RetryWorker,
                "event-worker" => RunnerCommand.EventWorker,
                _ => RunnerCommand.ObserverWorker,
            };

            if (args.Count == 1)
            {
                options = options with
                {
                    Command = workerCommand,
                };
                return true;
            }

            if (args.Count == 2 && IsHelp(args[1]))
            {
                options = options with
                {
                    Command = workerCommand,
                    ShowHelp = true,
                };
                return true;
            }

            error = $"{command} does not accept additional arguments.";
            return false;
        }

        if (command == "event")
        {
            return TryParseEvent(
                args,
                options,
                out options,
                out error);
        }

        if (command == "observer")
        {
            if (args.Count == 4 &&
                string.Equals(
                    args[1],
                    "import",
                    StringComparison.OrdinalIgnoreCase) &&
                args[2] == "--file" &&
                !string.IsNullOrWhiteSpace(args[3]))
            {
                options = options with
                {
                    Command = RunnerCommand.ObserverImport,
                    ObserverPath = args[3],
                };
                return true;
            }

            if (args.Count == 2 && IsHelp(args[1]))
            {
                options = options with
                {
                    Command = RunnerCommand.ObserverImport,
                    ShowHelp = true,
                };
                return true;
            }

            error = "observer import requires --file <path>.";
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

        if (command is "resume" or "cancel")
        {
            if (args.Count == 2 && IsHelp(args[1]))
            {
                options = options with
                {
                    Command = command == "resume"
                        ? RunnerCommand.Resume
                        : RunnerCommand.Cancel,
                    ShowHelp = true,
                };
                return true;
            }

            if (args.Count == 3 &&
                args[1] == "--run-id" &&
                Guid.TryParse(args[2], out var runId) &&
                runId != Guid.Empty)
            {
                options = options with
                {
                    Command = command == "resume"
                        ? RunnerCommand.Resume
                        : RunnerCommand.Cancel,
                    RunId = runId,
                };
                return true;
            }

            error = $"{command} requires --run-id <guid>.";
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

    private static bool TryParseEvent(
        IReadOnlyList<string> args,
        RunnerCommandLine defaults,
        out RunnerCommandLine options,
        out string? error)
    {
        options = defaults;
        error = null;

        if (args.Count < 2)
        {
            error = "event requires the publish subcommand.";
            return false;
        }

        if (IsHelp(args[1]))
        {
            options = options with { ShowHelp = true };
            return true;
        }

        if (!string.Equals(
                args[1],
                "publish",
                StringComparison.OrdinalIgnoreCase))
        {
            error = $"Unknown event subcommand '{args[1]}'.";
            return false;
        }

        Guid? eventId = null;
        string? eventType = null;
        string? correlationId = null;
        string? payloadJson = null;

        for (var index = 2; index < args.Count; index++)
        {
            var argument = args[index];

            if (IsHelp(argument))
            {
                options = options with
                {
                    Command = RunnerCommand.EventPublish,
                    ShowHelp = true,
                };
                return true;
            }

            if (argument == "--id")
            {
                if (++index >= args.Count ||
                    !Guid.TryParse(args[index], out var parsedEventId) ||
                    parsedEventId == Guid.Empty)
                {
                    error = "--id requires a non-empty GUID.";
                    return false;
                }

                eventId = parsedEventId;
                continue;
            }

            if (argument == "--type")
            {
                if (++index >= args.Count ||
                    string.IsNullOrWhiteSpace(args[index]))
                {
                    error = "--type requires a non-empty value.";
                    return false;
                }

                eventType = args[index];
                continue;
            }

            if (argument == "--correlation")
            {
                if (++index >= args.Count ||
                    string.IsNullOrWhiteSpace(args[index]))
                {
                    error = "--correlation requires a non-empty value.";
                    return false;
                }

                correlationId = args[index];
                continue;
            }

            if (argument == "--payload")
            {
                if (++index >= args.Count ||
                    string.IsNullOrWhiteSpace(args[index]))
                {
                    error = "--payload requires a non-empty JSON value.";
                    return false;
                }

                payloadJson = args[index];
                continue;
            }

            error = $"Unknown event argument '{argument}'.";
            return false;
        }

        if (eventId is null ||
            string.IsNullOrWhiteSpace(eventType) ||
            string.IsNullOrWhiteSpace(correlationId))
        {
            error =
                "event publish requires --id <guid>, --type <type>, and --correlation <id>.";
            return false;
        }

        options = options with
        {
            Command = RunnerCommand.EventPublish,
            EventId = eventId,
            EventType = eventType,
            CorrelationId = correlationId,
            PayloadJson = payloadJson,
        };
        return true;
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
