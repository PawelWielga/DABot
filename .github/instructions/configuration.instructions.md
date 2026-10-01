---
applyTo: "config*.json,schemas/config.schema.json"
---

# Configuration instructions

- Use `schemas/config.schema.json` as the machine-readable contract for DABot configuration.
- Keep configuration keys aligned with the current `BotOptions` tree, including browser, storage, worker, and interactive-browser option groups.
- Do not put secrets, credentials, cookies, tokens, or browser profile data in committed configuration files.
- The Web management editor owns non-secret general and storage runtime settings in its `appsettings.json`; section-specific saves must preserve all other sections and unknown fields, use the shared file-write lock, and state clearly that saved changes require a process restart.
- Storage-path changes are configuration-only: validate paths, require explicit acknowledgement in the UI, and never silently migrate, copy, or delete existing storage data.
- When configuration structure or defaults change, update the schema, README/Quick Start when relevant, tests, and examples in the same change.
