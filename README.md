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
- interpolate `{{variableName}}` references in string step inputs, including built-in `runId`,
- wait for selectors, text, URLs, and page load states,
- pause execution for a fixed delay,
- capture screenshots,
- call HTTP APIs with GET/POST/PUT and map response values into scenario outputs,
- execute scenarios through the .NET runner,
- cancel an active one-shot run with Ctrl+C,
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

The runner reads `config.json`, which points to `scenarios/sample-open-url.json` by default. A scenario path passed on the command line overrides `bot.scenarioPath` for that run.

CI performs the restore/build flow on Ubuntu, creates a framework-dependent `linux-x64` publish, starts the published runner, installs Chromium, and runs a self-contained browser scenario. The Linux deployment path is therefore continuously smoke-tested. See [Linux publish and smoke test](docs/linux-publish.md).

## Minimal scenario

A DABot scenario is a JSON document containing an ordered list of steps. The machine-readable contract is [schemas/scenario.schema.json](schemas/scenario.schema.json). Any step may set `enabled: false` to remain in the definition while being skipped at execution time; omitting `enabled` keeps the backward-compatible enabled behavior.

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

To run a different scenario without changing configuration:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- run --scenario examples/form-interaction.json
```

The shorter `-s` alias is also supported.

More examples are available in [examples](examples/README.md).

### Navigation synchronization

`OpenUrl` waits for the browser's normal `load` event. It does not use Playwright `NetworkIdle` as a universal navigation condition because modern pages may keep background requests open indefinitely.

When a workflow needs a stronger readiness condition, express it explicitly with a following `WaitFor` step, for example a selector, text, URL, or load-state wait. This keeps synchronization tied to what the scenario actually needs instead of assuming that network silence means the page is ready.

### Scenario variables

String step inputs such as `url`, `selector`, `value`, and string-valued `parameters` can reference variables with `{{variableName}}`.

Variables currently come from:

- outputs produced by earlier steps such as `ReadText`,
- initial variables supplied to a durable `ScenarioRunRequest`,
- the built-in `runId` variable for the current execution.

An undefined variable fails the step instead of silently leaving the placeholder in place. The one-shot CLI does not yet expose separate `--var` arguments, so ordinary runner scenarios primarily use outputs from earlier steps and `runId`.


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
| Fixed delays | Available |
| Screenshots and step results | Available |
| CLI-style .NET runner | Available |
| Structured logging and failure artifacts | Available |
| HTTP/API scenario steps | Available |
| Variable interpolation | Available |
| Durable retry recovery/scheduling | Available (single-worker) |
| Conditions and loops | Available |
| Persistent named browser profiles | Available |
| Durable run persistence | Available |
| Suspend and resume | Available |
| External event model | Available |
| Generic page observers | Available |
| Web management panel | Dashboard, runs, synchronized visual/JSON scenario editing, browser profile management, local headed interactive profile setup, observer monitoring/configuration, event history/manual publishing, and restart-required general/storage configuration editing available |
| MCP server/client integration | Planned |
| Dynamic reusable tool registry | Planned |

The detailed implementation state is tracked in [docs/tasks.md](docs/tasks.md).

The Blazor management panel is available:

```bash
dotnet run --project src/DesktopAutomationBot.Web
```

It reads durable run, observer, and event-inbox state through Application services, can create/edit page observers, browse event delivery/resume metadata, manually publish idempotent durable events, manages persistent browser profiles, can start/stop a headed interactive profile browser, and can edit non-secret general and storage settings for the next process start through Application services. Storage path changes are configuration-only and never migrate existing files; the Web project still does not directly own Playwright. See [web management panel](docs/web-management.md).

A static demo of the same management UI, backed only by sample data, is published on GitHub Pages: [DABot web demo](https://pawelwielga.github.io/DABot/). The demo does not execute automation or connect to a runtime.

## Execution model

DABot supports both the original one-shot execution path and durable persisted runs.

One-shot scenarios execute synchronously and remain the simplest option for short automations:

```text
Start -> Step -> Step -> Step -> Completed
```

Durable runs persist their scenario version, execution cursor, variables, step attempts, wait state, and event correlation data in SQLite. A durable workflow can suspend, let the process exit, and continue later:

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

Short browser waits remain active Playwright waits. `Suspend` is for long-lived waits that must survive process termination. Durable runs can also be resumed manually by `RunId`, retried by the retry worker, or resumed from idempotent external events.

See [durable workflow architecture](docs/durable-workflows.md).

## Architecture

The current solution is split into:

- `DesktopAutomationBot.Core` - scenario/domain models and validation,
- `DesktopAutomationBot.Application` - execution orchestration and step handlers,
- `DesktopAutomationBot.Infrastructure` - Playwright and infrastructure adapters,
- `DesktopAutomationBot.Runner` - executable composition root,
- `DesktopAutomationBot.Web` - optional server-hosted Blazor management client over Application services,
- `DesktopAutomationBot.Web.Shared` - shared Razor UI used by both the real web host and the static demo,
- `DesktopAutomationBot.Web.Demo` - Blazor WebAssembly demo host with sample-only data for GitHub Pages.

Planned clients and adapters include:

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
dotnet run --project src/DesktopAutomationBot.Runner -- run --scenario examples/form-interaction.json
```

Use `--help` or `-h` to print the CLI contract. `--scenario` (or `-s`) overrides `bot.scenarioPath` only for the current run.

Persistent browser profiles can be prepared and verified directly from the CLI:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- profile setup --profile work-account --url https://example.com/login
dotnet run --project src/DesktopAutomationBot.Runner -- profile test --profile work-account
```

Interactive setup forces a headed browser so login/MFA can be completed manually; the test command verifies that the profile can be exclusively acquired and launched headlessly.

Durable retries can be processed continuously with:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- retry-worker
```

Manual durable-run control uses the persisted run ID:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- resume --run-id <guid>
dotnet run --project src/DesktopAutomationBot.Runner -- cancel --run-id <guid>
```

Manual `resume` is for non-retry waiting runs. `cancel` currently accepts queued/waiting runs; cancelling an actively running process is deliberately rejected until worker lease/CAS coordination is implemented.

Durable events can be injected and processed without an external transport adapter:

```bash
dotnet run --project src/DesktopAutomationBot.Runner -- event publish --id <guid> --type order.approved --correlation order-123 --payload '{"approved":true}'
dotnet run --project src/DesktopAutomationBot.Runner -- event-worker

dotnet run --project src/DesktopAutomationBot.Runner -- observer import --file examples/observers/page-observer.json
dotnet run --project src/DesktopAutomationBot.Runner -- observer-worker
```

Event delivery is idempotent by `EventId`; the worker resumes matching persisted `Suspend(reason: Event)` runs from durable work items. Failed resume work items use persisted exponential backoff and move to a durable dead-letter queue after the configured maximum attempts.

The retry worker polls SQLite using `bot.retryWorker.pollIntervalMs` and processes at most `bot.retryWorker.batchSize` due runs per sweep. Ctrl+C requests a graceful stop: an active durable retry is allowed to finish, then the worker stops before starting the next run.

The current retry worker is intentionally single-worker. Do not run multiple retry workers against the same durable store until run leases / compare-and-swap claiming are implemented.

### Runner exit codes

The runner returns stable process exit codes so shell scripts, CI jobs, and service wrappers can distinguish failure classes:

| Code | Meaning |
| ---: | --- |
| `0` | Successful scenario execution or graceful retry-worker stop |
| `1` | Unexpected/internal runner error |
| `2` | Invalid CLI usage or unsupported command |
| `3` | Invalid or missing configuration/scenario input |
| `4` | Scenario execution completed with a failure |
| `130` | One-shot scenario execution was cancelled, for example with Ctrl+C |

Treat non-zero codes as failures. Code `3` is intended for problems that can be corrected before execution, while code `4` means the scenario started but did not complete successfully. Ctrl+C cancels an active one-shot run cooperatively and returns code `130`; the retry worker keeps its existing graceful-stop behavior and exits with `0`.

## Documentation

- [Documentation index](docs/README.md)
- [Contributing guide](CONTRIBUTING.md)
- [Scenario JSON Schema](schemas/scenario.schema.json)
- [Configuration JSON Schema](schemas/config.schema.json)
- [Product requirements](docs/prd.md)
- [Product positioning and differentiation](docs/product-positioning.md)
- [Implementation backlog](docs/tasks.md)
- [Linux publish and smoke test](docs/linux-publish.md)
- [Durable workflow architecture](docs/durable-workflows.md)
- [Distributed DABot deployment](docs/distributed-deployment.md)
- [MCP and dynamic tools architecture](docs/mcp-and-dynamic-tools.md)
- [Scenario schema and migration policy](docs/scenario-schema.md)
- [Architecture review and improvement plan](docs/architecture-review-2026-09-27.md)
- [DABot compared with raw Playwright](docs/comparison.md)
- [v0.1.0 release notes](docs/releases/v0.1.0.md)
- [Agent guidelines](AGENTS.md)
- [Runnable examples](examples/README.md)

## Roadmap

The roadmap is maintained as an implementation backlog rather than a marketing feature list. Planned work includes completing the web management panel, a distributed control plane with multiple VM/host agents and one shared dashboard, safe multi-worker coordination, operations/deployment features, scheduling, and MCP/tool-registry integration.

See [docs/tasks.md](docs/tasks.md) for acceptance criteria and implementation order.

## License

DABot is available under the [MIT License](LICENSE).
