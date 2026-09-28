# DABot compared with raw Playwright

DABot is not a replacement for Microsoft Playwright. It uses Playwright for browser control and adds an application/workflow layer around it.

Use this comparison to decide whether the extra layer is useful for a particular automation.

| Capability | Raw Playwright | DABot |
| --- | --- | --- |
| Browser navigation and interaction | Yes | Yes, through Playwright |
| Automation written directly in application code | Yes | Possible through the underlying .NET projects, but not the primary scenario model |
| Declarative JSON scenarios | Build it yourself | Available |
| Reusable scenario runner | Build it yourself | Available |
| Screenshots and per-step execution results | Build it yourself | Available |
| Persistent named browser profiles | Build it yourself | Planned |
| Durable run persistence across process restarts | Build it yourself | Planned |
| Suspend and resume on external events | Build it yourself | Planned |
| Generic page observers | Build it yourself | Planned |
| Web management panel | Build it yourself | Planned |
| MCP server/client integration | Build it yourself | Planned |
| Dynamic reusable tool registry | Build it yourself | Planned |

## Choose raw Playwright when

Raw Playwright is usually the simpler choice when the automation is a small code-first script, the lifecycle is short, and the application already owns orchestration, persistence, retries, observability, and integration boundaries.

## Choose DABot when

DABot is intended for cases where browser operations should be described as reusable scenarios and executed by a shared engine rather than embedded separately in every application.

The long-term direction is especially relevant when workflows must survive process restarts, wait for external events, reuse authenticated browser profiles, expose controlled automation capabilities to applications or agents, or be managed through more than one client such as CLI, HTTP, web UI, workers, and MCP.

## Current status matters

Only features marked as available in the main README should be treated as implemented. Planned capabilities are documented so contributors can understand the architecture and roadmap, but they should not be assumed to work yet.

See also:

- [README](../README.md)
- [Product requirements](prd.md)
- [Implementation backlog](tasks.md)
- [Durable workflow architecture](durable-workflows.md)
- [MCP and dynamic tools architecture](mcp-and-dynamic-tools.md)
