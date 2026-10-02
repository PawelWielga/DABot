---
applyTo: "config*.json,schemas/config.schema.json"
---

# Configuration instructions

- Use `schemas/config.schema.json` as the machine-readable contract for DABot configuration.
- Keep configuration keys aligned with the current `BotOptions` tree, including node, browser, storage, worker, and interactive-browser option groups.
- `bot.node.identityPath` points to machine-local runtime state. Its file must persist across restarts and must not be shared by distinct nodes.
- `bot.node.displayName` is optional and falls back to the machine name. `tags` and `capabilities` are normalized case-insensitively; `executionSlots` must be greater than zero and is currently reporting metadata, not a scheduler limit.
- `bot.node.heartbeatIntervalSeconds` must be greater than zero and controls persisted heartbeats for Web and long-running workers; changing it does not itself define liveness thresholds.
- Do not put secrets, credentials, cookies, tokens, or browser profile data in committed configuration files.
- The Web management editor owns non-secret general and storage runtime settings in its `appsettings.json`; section-specific saves must preserve all other sections and unknown fields, use the shared file-write lock, and state clearly that saved changes require a process restart.
- Storage-path changes are configuration-only: validate paths, require explicit acknowledgement in the UI, and never silently migrate, copy, or delete existing storage data.
- When configuration structure or defaults change, update the schema, README/Quick Start when relevant, tests, and examples in the same change.
