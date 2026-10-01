---
applyTo: "config*.json,schemas/config.schema.json"
---

# Configuration instructions

- Use `schemas/config.schema.json` as the machine-readable contract for DABot configuration.
- Keep configuration keys aligned with the current `BotOptions` tree, including browser, storage, worker, and interactive-browser option groups.
- Do not put secrets, credentials, cookies, tokens, or browser profile data in committed configuration files.
- The Web management editor currently owns only non-secret general runtime settings in its `appsettings.json`; preserve storage, logging, and unknown sections and state clearly that saved changes require a process restart.
- When configuration structure or defaults change, update the schema, README/Quick Start when relevant, tests, and examples in the same change.
