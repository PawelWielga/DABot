# Contributing to DABot

Thanks for improving DABot.

Before changing code, read [AGENTS.md](AGENTS.md). It is the authoritative repository-level guide for architecture, product direction, testing, documentation, and public-repository maintenance.

## Development setup

Requirements:

- .NET 8 SDK
- PowerShell 7 (`pwsh`) to run the Playwright browser installation script
- Git

From the repository root:

```bash
dotnet restore
dotnet build
dotnet test
```

For browser-backed development, install Chromium after a Debug build:

```bash
pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install chromium
```

On Linux, Playwright can also install required system dependencies:

```bash
pwsh src/DesktopAutomationBot.Infrastructure/bin/Debug/net8.0/playwright.ps1 install --with-deps chromium
```

Run the current sample:

```bash
dotnet run --project src/DesktopAutomationBot.Runner
```

## Where changes belong

- `DesktopAutomationBot.Core`: scenario/domain models and validation.
- `DesktopAutomationBot.Application`: orchestration, use cases, interfaces, and step handlers.
- `DesktopAutomationBot.Infrastructure`: Playwright and other infrastructure adapters.
- `DesktopAutomationBot.Runner`: executable composition and CLI/worker startup.
- `tests/`: unit and integration tests.

Do not move website-, vendor-, transport-, Playwright-, or MCP-specific types into Core.

## Scenario files

The machine-readable scenario contract is [schemas/scenario.schema.json](schemas/scenario.schema.json). The committed runtime configuration is described by [schemas/config.schema.json](schemas/config.schema.json).

Runnable examples live under [examples](examples/README.md). When a scenario-facing capability changes:

1. update the domain model/validation as required,
2. update the JSON Schema,
3. add or update examples,
4. update tests that load repository scenarios,
5. update README feature status so planned functionality is not presented as released.

Current executable step handlers are:

- `OpenUrl`
- `Click`
- `FillText`
- `PasteText`
- `ReadText`
- `WaitFor`
- `Delay`
- `Screenshot`

Other values may exist in the domain model before their executable handlers are implemented. Do not treat that as released support.

## Pull requests

Keep PRs small enough to review as one coherent change. A PR should explain:

- the problem or requirement,
- the approach,
- observable behavior changes,
- compatibility implications,
- tests or validation performed,
- documentation and examples updated.

Use the repository pull request template.

## Definition of done

A change is not complete merely because it compiles. When relevant, it must also include:

- tests for behavior changes,
- Linux/headless compatibility,
- README status updates,
- Quick Start corrections,
- example updates,
- JSON Schema updates for scenarios or configuration,
- architecture/backlog/PRD updates,
- CI changes,
- release-readiness checklist updates,
- repository metadata review when product scope materially changes.

## Security and secrets

Never commit credentials, tokens, cookies, browser profiles, or other secret runtime data. Do not place secrets in logs, examples, test fixtures, issue bodies, or pull request descriptions.

## AI-assisted contributions

AI-assisted changes are welcome, but the submitted change must still be verified against the repository's code, tests, architecture rules, and current implementation status. Generated documentation must not claim planned functionality is already available.
