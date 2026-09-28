# DABot examples

These scenarios use only step types that are currently registered by the DABot application layer. Each JSON file references `../schemas/scenario.schema.json` through `$schema` so editors and agents can validate the scenario contract.

To run an example today, point `bot.scenarioPath` in the repository-root `config.json` at the selected JSON file, then run the runner from the repository root:

```bash
dotnet run --project src/DesktopAutomationBot.Runner
```

Examples:

- `basic-navigation.json` opens a public page, waits for content, reads text, and captures a screenshot.
- `form-interaction.json` uses a self-contained `data:` page to demonstrate filling a field, clicking a button, waiting for text, and reading the result.
- `paste-text.json` uses a self-contained `data:` page to demonstrate paste-style input, reading text, and a screenshot.
- `delay.json` uses a self-contained `data:` page to demonstrate a fixed millisecond delay between browser steps.
- `variable-interpolation.json` reads a value into an output and reuses it, together with the built-in `runId`, in a later step.\n- `call-api.json` calls a public JSON API with GET and maps a JSON response property into an output variable.

The self-contained examples avoid depending on a third-party test website.

When adding or changing a scenario-facing feature, update the schema, examples, runtime validation tests, README project status, and backlog together.
