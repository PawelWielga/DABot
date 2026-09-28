# DABot examples

These scenarios use only step types that are currently registered by the DABot application layer.

To run an example today, point `bot.scenarioPath` in the repository-root `config.json` at the selected JSON file, then run the runner from the repository root:

```bash
dotnet run --project src/DesktopAutomationBot.Runner
```

Examples:

- `basic-navigation.json` opens a public page, waits for content, reads text, and captures a screenshot.
- `form-interaction.json` uses a self-contained `data:` page to demonstrate filling a field, clicking a button, waiting for text, and reading the result.
- `paste-text.json` uses a self-contained `data:` page to demonstrate paste-style input, reading text, and a screenshot.

The self-contained examples avoid depending on a third-party test website.
