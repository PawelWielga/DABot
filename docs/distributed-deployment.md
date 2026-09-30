# Distributed DABot deployment

## Purpose

DABot must support two deployment shapes without creating separate execution engines:

1. **Standalone** - one host runs the web panel, Application services, workers, Playwright, and SQLite.
2. **Distributed** - one central DABot Server / Control Plane manages multiple DABot Agent nodes running on separate VMs or physical machines.

The distributed model exists so several DABot runtimes can share one dashboard, one run queue, and one operational view while browser execution stays close to the machine that owns the browser session and persistent profile.

The standalone mode remains a first-class deployment option and must not require the distributed control plane.

## Target topology

```text
                       DABot Server / Control Plane
                    +-------------------------------+
                    | Web dashboard                 |
                    | HTTP API                      |
                    | Scheduler / dispatcher        |
                    | Node registry                 |
                    | Durable run coordination      |
                    | Central persistence           |
                    +---------------+---------------+
                                    |
                         outbound agent connections
                                    |
              +---------------------+---------------------+
              |                     |                     |
              v                     v                     v
        DABot Agent 1         DABot Agent 2         DABot Agent 3
        Linux VM              Linux VM              Windows VM
        Playwright            Playwright            Playwright
        local profiles        local profiles        local profiles
        local temp data       local temp data       local temp data
```

A node may host one or more execution slots, but concurrency must be explicitly configured and bounded.

## Responsibilities

### DABot Server / Control Plane

The central server owns cluster-wide coordination:

- dashboard and management API,
- durable run metadata,
- scenario and scenario-version metadata,
- central work queue / dispatch state,
- node registry,
- node heartbeats and liveness state,
- run-to-node assignment,
- leases and compare-and-swap claiming,
- scheduling decisions,
- audit history,
- cluster-wide metrics and health.

The server must use the same Application use cases as CLI, workers, and future MCP adapters. It must not directly control Playwright.

### DABot Agent / Node

An agent owns machine-local execution concerns:

- Playwright browser execution,
- ephemeral browser sessions,
- persistent browser-profile directories,
- local profile locking,
- local temporary artifacts before upload,
- execution-slot limits,
- node capability reporting,
- heartbeat emission,
- claiming or receiving assigned work,
- lease renewal while work is active,
- reporting progress, results, diagnostics, and failures.

The agent should be able to run without accepting inbound public connections. The preferred network shape is that the agent establishes an outbound authenticated connection to the control plane.

## Communication model

The first implementation may use HTTPS polling:

```text
Agent -> POST heartbeat
Agent -> request next eligible job
Agent -> renew run lease
Agent -> report progress/result
Agent -> upload artifact metadata or content
```

A later implementation may replace or supplement polling with SignalR/WebSocket or a queue transport. Transport details remain Infrastructure concerns.

The Core model must not depend on HTTP, SignalR, RabbitMQ, Kafka, or another specific transport.

## Node identity and capabilities

Each node has a stable `NodeId` and reports metadata such as:

- display name,
- operating system,
- DABot version,
- Playwright/browser versions,
- configured execution-slot count,
- current active-run count,
- optional CPU and memory load,
- supported browser types,
- configured tags/capabilities,
- last heartbeat,
- current lifecycle status.

Example capabilities:

```text
linux
chromium
edge
interactive
profile:allegro
region:home-lab
```

Scenario/run dispatch may eventually declare required capabilities. The scheduler must only assign work to eligible nodes.

## Node lifecycle

Recommended node states:

```text
Online
Draining
Offline
Disabled
Unhealthy
```

Semantics:

- **Online** - eligible for new work.
- **Draining** - finishes current work but receives no new runs.
- **Offline** - heartbeat expired.
- **Disabled** - administratively excluded from scheduling.
- **Unhealthy** - connected but currently unable to execute work safely.

Heartbeat expiry must not immediately re-execute an in-flight side effect. The run lease and persisted step-attempt recovery rules still apply.

## Run dispatch and leases

Multiple nodes must never be allowed to execute the same mutable run concurrently.

A distributed claim requires at least:

```text
RunId
LeaseOwnerNodeId
LeaseToken / version
LeaseAcquiredAt
LeaseUntil
```

Claiming must be atomic through a database transaction, compare-and-swap operation, or equivalent concurrency primitive.

Rules:

1. only an eligible queued/due run can be claimed,
2. one active lease exists for a run at a time,
3. the owning agent periodically renews the lease,
4. progress writes verify lease ownership,
5. an expired lease enters recovery before the run can be executed again,
6. unsafe interrupted attempts continue to use the existing `Waiting / Human` recovery semantics,
7. cancellation and retry actions from the dashboard must respect active lease ownership.

## Persistent browser profiles

Persistent browser profiles remain machine-local by default.

A profile identity therefore needs an ownership/location concept such as:

```text
ProfileId
NodeId
LocalProfileName
Status
LeaseOwnerRunId
```

The scheduler must route a run that requires a node-local profile to the node that owns that profile.

DABot must not place an active Chromium user-data directory on generic shared network storage as a way to move profiles between nodes. Profile migration, backup, or replication is a separate explicit operation.

## Storage strategy

### Standalone

The existing model remains valid:

```text
Web + workers + Playwright + SQLite
```

SQLite remains the preferred simple local store.

### Distributed

A shared database service is preferred for cluster-wide coordination:

```text
DABot Server
    |
PostgreSQL
    |
Node registry / runs / leases / events / schedules / audit
```

PostgreSQL is the preferred target for the first supported distributed store because the control plane requires safe concurrent writers, transactional claiming, and central visibility.

SQLite must not be placed on a network share and used as a multi-VM coordination database.

Persistence stays behind Application/Core interfaces so the standalone SQLite implementation and distributed PostgreSQL implementation can coexist.

## Artifacts

Run metadata is central. Large artifacts may use one of two models:

- upload to central server/object storage after capture,
- remain node-local temporarily with a central reference until collected.

The dashboard must expose one logical artifact view regardless of physical storage.

Artifact transfer must preserve the stable `RunId`, step identity, timestamps, and content metadata.

## Dashboard

The shared dashboard should gain a **Nodes / Workers** section showing at minimum:

- node name and `NodeId`,
- status,
- last heartbeat,
- DABot version,
- OS,
- capabilities/tags,
- execution-slot usage,
- active runs,
- recent failures,
- drain/enable/disable controls.

Run detail should show:

- assigned/current node,
- lease state,
- execution history across nodes,
- recovery reason when ownership changes after failure.

The main dashboard should aggregate runs and node health across the whole cluster.

## Security

Distributed mode requires explicit machine authentication.

Minimum expectations:

- TLS for agent-server communication,
- per-node credentials or certificates,
- credential rotation,
- server-side node authorization,
- no browser/session secrets in heartbeat payloads,
- audit log for node enable/disable/drain and manual run actions,
- least-privilege access for agents.

Opening inbound agent ports should not be required for the normal topology.

## Compatibility rules

- Existing one-shot CLI scenarios continue to work.
- Existing standalone durable runtime continues to work with SQLite.
- Web remains optional for standalone execution.
- Distributed mode reuses the same Application execution semantics.
- Core does not depend on a network transport or database vendor.
- A scenario definition does not need to know the physical VM unless it explicitly requests capabilities or a node-bound browser profile.
- Historical runs retain their scenario version and node-execution history.

## Implementation sequence

The distributed deployment should be implemented incrementally:

1. introduce stable worker/node identity and registry,
2. add heartbeat and liveness,
3. add atomic run leasing / compare-and-swap claiming,
4. make current retry/event/observer workers lease-aware,
5. add node execution-slot limits,
6. add node capabilities and capability-aware dispatch,
7. add node-aware browser-profile ownership,
8. add Nodes / Workers dashboard,
9. introduce the distributed database provider, targeting PostgreSQL,
10. add authenticated agent-server API and outbound agent mode,
11. add artifact transfer/central references,
12. add drain/disable/recovery operations,
13. run multi-node concurrency and failure-recovery tests,
14. document production deployment and upgrade procedures.

## Acceptance criteria

The distributed MVP is complete when:

- at least two DABot agents on separate VMs can connect to one control plane,
- both agents are visible in one dashboard,
- a run can be submitted centrally and executed by exactly one eligible agent,
- simultaneous claiming cannot cause duplicate execution,
- a lost agent heartbeat expires its lease and invokes safe recovery semantics,
- persistent node-local profiles are never concurrently used by two runs,
- the dashboard shows the node assigned to each active/recent run,
- standalone SQLite deployment still works without the control plane,
- distributed deployment uses a supported shared persistence strategy rather than shared-file SQLite.
