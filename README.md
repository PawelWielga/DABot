# DABot

DABot (Desktop Automation Bot) is a .NET-based browser automation engine built around declarative scenarios, Playwright, durable runs, and external events.

The project is designed to stay useful as a general automation tool rather than being tied to one website or one business process.

## Main capabilities

- JSON-defined browser scenarios
- Playwright-based browser automation
- synchronous and durable execution
- suspend and resume of long-running workflows
- persistent browser profiles
- external events and correlation
- page observation without blocking the main workflow
- REST/API integration
- screenshots, HTML snapshots, logs, and diagnostics
- CLI/worker operation on Linux
- optional web panel for configuration and monitoring

## Architecture

The main layers are:

- `DesktopAutomationBot.Core` - domain models and validation
- `DesktopAutomationBot.Application` - use cases and orchestration
- `DesktopAutomationBot.Infrastructure` - Playwright, persistence, integrations, event transport
- `DesktopAutomationBot.Runner` - CLI/worker composition root
- `DesktopAutomationBot.Web` - planned web management panel

The web panel is a client of the same application layer as the CLI. It must not become a requirement for running scenarios.

## Execution model

A scenario can finish in one process:

```text
Start -> steps -> Completed
```

or suspend while waiting for an external condition:

```text
Start -> steps -> Suspend
                    |
                 process exits
                    |
              external event
                    |
                  Resume
                    |
               next steps
```

Short browser waits such as waiting for a selector remain normal blocking steps. Long waits are represented as durable suspension and later resume.

## Runner modes

The runner keeps the existing one-shot scenario execution as the default:

```bash
dotnet run --project src/DesktopAutomationBot.Runner
dotnet run --project src/DesktopAutomationBot.Runner -- run
```

Durable retries can be processed continuously with:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- retry-worker
```

The retry worker polls SQLite using `bot.retryWorker.pollIntervalMs` and processes at most `bot.retryWorker.batchSize` due runs per sweep. Ctrl+C requests a graceful stop: an active durable retry is allowed to finish, then the worker stops before starting the next run.

The current retry worker is intentionally single-worker. Do not run multiple retry workers against the same durable store until run leases / compare-and-swap claiming are implemented.

## Documentation

- [Product requirements](docs/prd.md)
- [Implementation backlog](docs/tasks.md)
- [Durable workflow architecture](docs/durable-workflows.md)
- [Scenario schema and migration policy](docs/scenario-schema.md)
- [Architecture review and improvement plan](docs/architecture-review-2026-09-27.md)
- [Agent guidelines](AGENTS.md)
