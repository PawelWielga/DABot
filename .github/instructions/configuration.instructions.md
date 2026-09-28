---
applyTo: "config*.json,schemas/config.schema.json"
---

# Configuration instructions

- Use `schemas/config.schema.json` as the machine-readable contract for DABot configuration.
- Keep configuration keys aligned with `BotOptions`, `BrowserOptions`, and `StorageOptions`.
- Do not put secrets, credentials, cookies, tokens, or browser profile data in committed configuration files.
- When configuration structure or defaults change, update the schema, README/Quick Start when relevant, tests, and examples in the same change.
