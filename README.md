# DABot

[![CI](https://github.com/PawelWielga/DABot/actions/workflows/ci.yml/badge.svg)](https://github.com/PawelWielga/DABot/actions/workflows/ci.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![.NET 8](https://img.shields.io/badge/.NET-8.0-512BD4.svg)](https://dotnet.microsoft.com/)

**DABot (Desktop Automation Bot)** is a self-hosted .NET browser automation engine powered by Microsoft Playwright.

It adds a declarative JSON scenario layer and a reusable execution engine on top of Playwright. The project is being developed toward durable, event-driven workflows that can be invoked from multiple clients without coupling the automation logic to one website, vendor, or AI provider.

> DABot is not a replacement for Playwright. Playwright provides the browser automation primitives; DABot provides the scenario and workflow layer around them.

## What works today

DABot can currently:

- load browser scenarios from JSON,
- open URLs,
- click elements,
- fill and paste text,
- read text into scenario outputs,
- wait for selectors, text, URLs, and page load states,
- capture screenshots,
- execute scenarios through the .NET runner,
- validate scenario definitions before execution.

The roadmap intentionally goes further, but planned features are marked separately below so they are not mistaken for released functionality.

## When DABot is a good fit

DABot is aimed at automation where:

- a website has no suitable API,
- browser interaction should be described as reusable data rather than duplicated application code,
- the automation should stay self-hosted,
- several applications or clients should eventually use the same execution layer,
- authenticated browser sessions, durable state, external events, or long-running workflows will matter,
- automation capabilities may later be exposed to applications or agents through controlled interfaces.

For a small code-only browser script, raw Playwright will often be the simpler choice. See [DABot compared with raw Playwright](docs/comparison.md).

## Quick start

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- PowerShell 7 (`pwsh`) for the Playwright browser-install script

Clone and build the repository:

```bash
git clone https://github.com/PawelWielga/DABot.git
cd DABot
dotnet restore
dotnet build
```

Install Chromium for Playwright:

```bash
pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install chromium
```

On a Linux machine that also needs browser system dependencies, Playwright can install them together with Chromium:

```bash
pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install --with-deps chromium
```

Run the default sample from the repository root:

```bash
dotnet run --project src/DesktopAutomationBot.Runner
```

The runner reads `config.json`, which points to `scenarios/sample-open-url.json` by default.

CI performs the same restore/build/browser-install flow on Ubuntu and then runs a self-contained browser scenario, so the documented setup path is continuously smoke-tested.

## Minimal scenario

A DABot scenario is a JSON document containing an ordered list of steps. The machine-readable contract is [schemas/scenario.schema.json](schemas/scenario.schema.json).

```json
{
  "$schema": "./schemas/scenario.schema.json",
  "name": "Read example heading",
  "steps": [
    {
      "type": "OpenUrl",
      "url": "https://example.com"
    },
    {
      "type": "WaitFor",
      "selector": "h1"
    },
    {
      "type": "ReadText",
      "selector": "h1",
      "output": "heading"
    },
    {
      "type": "Screenshot"
    }
  ]
}
```

To run a different scenario with the current runner, change `bot.scenarioPath` in `config.json`.

More examples are available in [examples](examples/README.md).

## For agents and tooling

DABot keeps machine- and agent-readable project context in the repository:

- [AGENTS.md](AGENTS.md) - authoritative engineering and architecture instructions for coding agents,
- [.github/copilot-instructions.md](.github/copilot-instructions.md) - concise repository-wide GitHub Copilot instructions,
- [.github/instructions](.github/instructions) - path-specific instructions for .NET and scenario files,
- [Scenario JSON Schema](schemas/scenario.schema.json) - machine-readable scenario contract,
- [Configuration JSON Schema](schemas/config.schema.json) - machine-readable `config.json` contract,
- [Documentation index](docs/README.md) - map of current-status, architecture, and roadmap documents,
- [CONTRIBUTING.md](CONTRIBUTING.md) - development setup and definition of done.

Agents should use the README and implementation backlog to distinguish current functionality from roadmap design before generating code, scenarios, or recommendations.

## Project status

| Capability | Status |
| --- | --- |
| JSON scenario loading and validation | Available |
| Playwright browser control | Available |
| Open URL / click / fill / paste / read | Available |
| Wait for selector / text / URL / load state | Available |
| Screenshots and step results | Available |
| CLI-style .NET runner | Available |
| Structured logging and failure artifacts | Planned |
| HTTP/API scenario steps | Planned |
| Conditions, loops, retries, and variable interpolation | Planned |
| Persistent named browser profiles | Planned |
| Durable run persistence | Planned |
| Suspend and resume | Planned |
| External event model | Planned |
| Generic page observers | Planned |
| Web management panel | Planned |
| MCP server/client integration | Planned |
| Dynamic reusable tool registry | Planned |

The detailed implementation state is tracked in [docs/tasks.md](docs/tasks.md).

## Execution model

The current engine executes a scenario synchronously:

```text
Start -> Step -> Step -> Step -> Completed
```

The planned durable execution model extends the same engine so a run can persist its state, stop the process, and resume later:

```text
Start -> Step -> Suspend
                    |
              state persisted
                    |
               process exits
                    |
             external event
                    |
                  Resume
                    |
                 Step...
```

Short browser waits remain active Playwright waits. Long waits are planned as durable suspension rather than keeping a process blocked.

See [durable workflow architecture](docs/durable-workflows.md).

## Architecture

The current solution is split into:

- `DesktopAutomationBot.Core` - scenario/domain models and validation,
- `DesktopAutomationBot.Application` - execution orchestration and step handlers,
- `DesktopAutomationBot.Infrastructure` - Playwright and infrastructure adapters,
- `DesktopAutomationBot.Runner` - executable composition root.

Planned clients and adapters include:

- `DesktopAutomationBot.Web` - optional web management panel,
- `DesktopAutomationBot.Mcp` - MCP server/client adapter and dynamic tool registry integration.

All clients are intended to converge on the same Application use cases rather than create separate execution engines.

## Design principles

- **General-purpose automation** - Core must not depend on one website or business process.
- **Declarative scenarios** - reusable automation is expressed as data where practical.
- **One execution layer** - CLI, HTTP, web UI, workers, MCP, and agents should share the same use cases.
- **Self-hosted and Linux-first** - Linux is the primary runtime target; Windows remains supported for development and testing.
- **Durability by design** - long-running workflows should eventually survive process or machine restarts.
- **Controlled extensibility** - future agent-authored tools compose approved actions rather than execute unrestricted generated code.
- **No secrets in repository files or logs**.

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

- [Documentation index](docs/README.md)
- [Contributing guide](CONTRIBUTING.md)
- [Scenario JSON Schema](schemas/scenario.schema.json)
- [Configuration JSON Schema](schemas/config.schema.json)
- [Product requirements](docs/prd.md)
- [Implementation backlog](docs/tasks.md)
- [Durable workflow architecture](docs/durable-workflows.md)
- [MCP and dynamic tools architecture](docs/mcp-and-dynamic-tools.md)
- [Scenario schema and migration policy](docs/scenario-schema.md)
- [Architecture review and improvement plan](docs/architecture-review-2026-09-27.md)
- [DABot compared with raw Playwright](docs/comparison.md)
- [Agent guidelines](AGENTS.md)
- [Runnable examples](examples/README.md)

## Roadmap

The roadmap is maintained as an implementation backlog rather than a marketing feature list. Planned work includes complete runner diagnostics, API steps, persistent browser sessions, durable runs, suspend/resume, external events, observers, a web panel, and MCP/tool-registry integration.

See [docs/tasks.md](docs/tasks.md) for acceptance criteria and implementation order.

## License

DABot is available under the [MIT License](LICENSE).
