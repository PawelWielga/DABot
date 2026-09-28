# MCP and dynamic tools architecture

## Purpose

DABot should be usable as a general execution layer for humans, applications, and AI agents.

MCP is a standard integration boundary for that goal. It must not become a second automation engine and it must not couple Core to a particular AI provider, agent product, website, or MCP SDK.

The central design rule is:

> Agents may create reusable declarative tools from approved DABot Actions, but tool creation must not introduce an unrestricted arbitrary-code execution path.

## Conceptual model

DABot has two extension levels.

### Actions

Actions are trusted, low-level capabilities implemented and tested in code. Examples include browser navigation, click, fill/paste text, read text, browser-local waits, screenshots, HTTP requests, delays, conditions, loops, events, and explicitly permitted filesystem or process operations.

Actions are implementation primitives. They own concrete integration with Playwright, HttpClient, filesystem, process execution, and similar infrastructure.

### Tools / Workflows

A Tool is a named, versioned, declarative composition of Actions.

It has:

- stable identifier
- human-readable name and description
- version
- input schema
- output schema
- ordered workflow definition
- required permissions
- lifecycle status
- source/author metadata
- test status
- creation and modification timestamps

A Tool is reusable from CLI, HTTP, the web panel, other workflows, and MCP.

Example YAML:

    id: github.create_issue_browser
    version: 1
    inputs:
      repository:
        type: string
      title:
        type: string
      body:
        type: string
    steps:
      - action: browser.navigate
        with:
          url: https://github.com/{{repository}}/issues/new
      - action: browser.fill
        with:
          selector: "#issue_title"
          value: "{{title}}"
      - action: browser.fill
        with:
          selector: "#issue_body"
          value: "{{body}}"
      - action: browser.click
        with:
          selector: "button[type=submit]"
    outputs:
      url: "{{browser.currentUrl}}"

The exact serialization format may evolve. JSON remains a supported interchange format; YAML can be added for authoring if useful.

## One execution engine

All callers must converge on the same Application use cases:

    CLI -----------+
    HTTP ----------+
    Web -----------+----> Application ----> Core/runtime ----> Infrastructure
    MCP Server ----+
    Worker --------+

MCP must not bypass scenario/tool validation, permission checks, run tracking, cancellation, durable state, diagnostics, audit logging, or browser session ownership.

## DABot as an MCP Server

The MCP server exposes selected DABot capabilities to external agents.

Primary exposure:

- active Tools/Workflows from the registry
- optionally selected built-in system tools

Potential management tools include tool.list, tool.get, tool.create, tool.update, tool.validate, tool.test, tool.enable, and tool.disable.

A tool call is translated into an Application request and executed like any other DABot run.

The active MCP tool list is derived from the registry. When tools are enabled, disabled, created, or versioned, the MCP adapter should refresh the exposed list and notify clients when supported by the protocol/runtime.

## DABot as an MCP Client

DABot can also connect to configured external MCP servers. External MCP tools are infrastructure integrations; Core must not depend on MCP protocol types.

Each connection should define:

- server identity
- transport/configuration
- enabled/disabled state
- allowlisted tools or patterns
- timeout
- permissions
- secret references
- diagnostics policy

An external MCP server must not be able to silently escalate local DABot permissions.

## AI-authored tools

AI is one possible author of Tool definitions, not a privileged execution path.

Recommended lifecycle:

    Draft -> Validate -> Permission check -> Test -> Enable -> Version/Disable

Suggested policy modes:

1. Manual only: agents cannot create tools.
2. AI drafts: agents may create/update drafts; a human enables them.
3. AI safe auto-enable: agents may enable tools composed only of actions allowed by the current policy.
4. Fully autonomous: explicitly configured environment with broad permissions; still audited and bounded by runtime controls.

Default target policy for normal installations: AI safe auto-enable.

## Permission model

Permissions belong to capabilities, not to the UI that invoked them.

Example categories:

- browser.navigate
- browser.interact
- http.outbound
- filesystem.read
- filesystem.write
- process.execute
- git.read
- git.write
- credentials.use:<name>
- mcp.call:<server>

A Tool's effective permissions are the union of the permissions required by its Actions and external integrations.

High-risk permissions should be configurable as approval-required. Initial candidates are unrestricted process/shell execution, unrestricted filesystem write, credential access, unrestricted outbound network access, repository push/write, and destructive administrative operations.

## Validation and safety

Dynamic Tool creation must validate at least unique ID/name rules, schema correctness, known Action types, required step parameters, variable/output references, permission requirements, maximum steps/loops/timeouts, and recursion/cycle rules for Tool-to-Tool calls.

A Tool definition must never be treated as trusted executable source code merely because an AI generated it.

If custom executable plugins are added in the future, they need a separate plugin/security model and are out of scope for declarative Tool creation.

## Versioning

Tool edits create a new version. A running or historical run references the exact Tool version it executed. Updating a Tool must not change the definition of an already-started or historical run.

Recommended identity: ToolId + Version. A content hash can optionally verify immutable definitions.

## Testing

A tool test should be a normal controlled run with explicit test inputs, selected browser profile or ephemeral session, standard artifacts, timeout, permission checks, result/output capture, and no automatic production activation if the test fails.

## Web panel

Add a Tools area to the optional management panel.

The Tools list should show name, version, status, source, required permissions, last test result, and last modified date.

The editor should support metadata, input/output schema, a visual step editor, JSON, optional YAML, permission preview, validation, test, save draft, and enable/disable.

Tool detail should show current version, version history, tests, runs, audit trail, source/author, and effective permissions.

An MCP connections screen should show configured external MCP servers, connection health, allowed tools, permissions, and diagnostics.

## Suggested project boundaries

    DesktopAutomationBot.Core
      Actions/Tools domain models
      validation contracts
      permission semantics

    DesktopAutomationBot.Application
      tool registry use cases
      tool execution orchestration
      validation/test/enable/disable use cases
      MCP-neutral external tool abstractions

    DesktopAutomationBot.Infrastructure
      Action implementations
      persistence
      external MCP client implementation
      browser/http/filesystem/process adapters

    DesktopAutomationBot.Mcp
      MCP server
      protocol mapping
      dynamic tool exposure
      MCP client composition/configuration

    DesktopAutomationBot.Runner
      CLI/worker composition

    DesktopAutomationBot.Web
      tool management UI
      MCP connection UI

The exact namespaces may differ, but protocol-specific MCP types stay outside Core.

## Storage

Likely logical entities:

- Tools
- ToolVersions
- ToolTests
- ToolPermissions
- ToolAuditEvents
- McpConnections

Tool definitions can still support JSON import/export. Secrets for MCP connections or Actions are stored by reference through the secrets abstraction, not embedded in Tool definitions.

## Interaction with scenarios

Scenarios and Tools should share execution primitives instead of becoming competing models.

A practical evolution path:

1. Keep existing Scenario JSON compatible.
2. Introduce the neutral Action registry under existing step handlers.
3. Let a Tool reference the same Actions.
4. Allow scenarios to invoke Tools through a generic ExecuteTool/equivalent mechanism.
5. Later evaluate whether Scenario and Tool definitions can share one common workflow definition internally without forcing an early migration.

## Non-goals

This architecture does not require coupling DABot to one AI provider, promoting a particular website or AI UI, replacing Playwright with MCP, making the web panel mandatory, allowing AI to run arbitrary generated code, exposing every DABot capability to every MCP client, or trusting external MCP servers by default.

## Implementation order

Do not start with protocol plumbing.

Recommended order:

1. Stabilize the current scenario engine and browser session model.
2. Introduce a neutral Action registry.
3. Introduce versioned declarative Tools and permissions.
4. Add validation/test/enable lifecycle.
5. Expose Tools through an MCP Server adapter.
6. Add controlled MCP Client support.
7. Add web management for Tools and MCP connections.
8. Enable AI-authored Tool drafts and safe auto-enable according to policy.

This keeps MCP replaceable and ensures the reusable Tool model exists even when no AI or MCP client is connected.
