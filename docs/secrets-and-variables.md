# Runtime variables and secrets

DABot treats normal runtime variables and secrets as two different data classes.

## Runtime variables

Runtime variables are durable workflow state.

They may contain JSON-compatible values:

- strings,
- numbers,
- booleans,
- null,
- objects,
- arrays.

Durable runs persist these values in `Runs.VariablesJson`. They may therefore appear in administrative tooling, backups, diagnostics, or future run-history views and **must not be used to carry credentials or other secrets**.

String interpolation remains convenient:

- `{{name}}` inside text converts a structured value to its compact textual representation;
- an exact `{{name}}` reference used as a string-valued step parameter preserves the underlying JSON type.

For example, a numeric variable used as the complete parameter value remains a JSON number rather than becoming a quoted string.

## Secrets

Secrets are resolved at execution time through `ISecretProvider`.

The default infrastructure implementation is `EnvironmentSecretProvider`, which treats the configured secret name as an environment-variable name. Other providers can later map the same abstraction to a vault or platform secret store without changing Core or scenario persistence.

For `CallApi`, use:

```json
{
  "type": "CallApi",
  "url": "https://example.test/private",
  "parameters": {
    "bearerTokenSecret": "DABOT_API_TOKEN"
  }
}
```

The scenario stores only the reference name `DABOT_API_TOKEN`. The provider resolves its value immediately before the request.

`bearerTokenEnv` remains supported for backward compatibility, but `bearerTokenSecret` is the provider-neutral form for new scenarios.

## Persistence and redaction rules

DABot follows these rules:

1. Secret values are never copied into `ScenarioVariableBag` or `AutomationRun.Variables` by the secret-provider path.
2. Secret values are therefore not written to `Runs.VariablesJson`.
3. Scenario definitions and immutable scenario versions store only secret reference names, never resolved values.
4. Built-in secret resolution errors identify the missing reference name but do not include the resolved secret value.
5. Built-in API logging does not log bearer-token values.
6. File-log sensitive-value masking remains defense in depth. Environment variables listed under `logging.sensitiveValues.environmentVariables` are replaced with `***` if their current values appear in persisted log messages, exceptions, or structured string properties.
7. A caller that intentionally places a credential in a normal runtime variable has classified it as durable workflow state; DABot cannot make that value non-persistent after the fact. Secrets should instead be passed by reference through `ISecretProvider`.

These rules keep ordinary workflow data inspectable and durable while keeping credential material outside normal run persistence.
