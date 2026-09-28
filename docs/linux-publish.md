# Linux publish and smoke test

DABot targets Linux first. The supported deployment baseline is a framework-dependent `linux-x64` publish running on a machine with the .NET 8 runtime and the Playwright Chromium dependencies installed.

## Build a Linux publish

From the repository root:

```bash
dotnet publish src/DesktopAutomationBot.Runner/DesktopAutomationBot.Runner.csproj \
  --configuration Release \
  --runtime linux-x64 \
  --self-contained false \
  --output artifacts/publish/linux-x64
```

The resulting entry point is:

```text
artifacts/publish/linux-x64/DesktopAutomationBot.Runner
```

The host must have the .NET 8 runtime installed because the publish is framework-dependent.

## Install Chromium and Linux browser dependencies

Build the repository first, then install the Playwright-managed Chromium build and required system dependencies:

```bash
pwsh src/DesktopAutomationBot.Infrastructure/bin/Release/net8.0/playwright.ps1 install --with-deps chromium
```

For production images or hosts, run this during provisioning rather than on every DABot start.

## Configuration

The runner resolves `config.json` and optional `config.local.json` from its current working directory.

For a deployed instance, keep configuration and runtime data outside source control. In particular, do not commit credentials, browser profiles, SQLite runtime data, screenshots, or other run artifacts.

## Smoke test the published runner

A fast smoke test that does not require opening Chromium is to verify the published executable and CLI contract:

```bash
set +e
artifacts/publish/linux-x64/DesktopAutomationBot.Runner invalid-command
exit_code=$?
set -e

test "$exit_code" -eq 2
```

A successful check proves that the Linux apphost starts, loads the runtime, and returns the documented CLI usage exit code.

## Browser smoke test

After Chromium is installed, configure a self-contained scenario and execute the published runner from the repository root:

```bash
cat > config.local.json <<'JSON'
{
  "bot": {
    "scenarioPath": "examples/form-interaction.json"
  }
}
JSON

artifacts/publish/linux-x64/DesktopAutomationBot.Runner run
rm -f config.local.json
```

The command should exit with code `0`.

## CI coverage

The GitHub Actions CI workflow runs on Ubuntu and verifies all of the following:

- restore,
- Release build,
- automated tests,
- documented runner exit codes,
- `linux-x64` publish,
- startup of the published Linux runner,
- Playwright Chromium installation with Linux dependencies,
- a self-contained browser smoke scenario.

This keeps the documented Linux deployment path aligned with the repository.
