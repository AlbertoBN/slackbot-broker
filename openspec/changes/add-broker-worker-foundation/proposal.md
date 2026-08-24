## Why

The repository currently has only a placeholder `Program.cs`. The approved design in
[docs/slack-broker-prd-v2.md](../../../docs/slack-broker-prd-v2.md) describes a two-process
Slack-controlled broker/worker system, but none of it exists as code or as OpenSpec capabilities
yet. We need the foundational architecture — protocol, executor contract, scheduling, Slack
gateway, and worker dispatch — in place before any concrete executor (Claude CLI, Git CLI,
gRPC, ...) can be built against it. Concrete executors are intentionally out of scope for this
change; a `MockExecutor` stands in for them so the full request pipeline can be built, exercised,
and tested end-to-end first.

## What Changes

- Introduce the local IPC envelope and message set (`ExecutionRequest`, `ExecutionAccepted`,
  `ExecutionProgress`, `ExecutionCompleted`, `ExecutionFailed`, `ExecutionCancelled`,
  `CancelExecution`, `ExecutorStatusRequest`/`Response`, `ConnectExecutor`/`DisconnectExecutor`,
  `HealthPing`/`HealthPong`) carried as framed UTF-8 JSON over a persistent Unix domain socket.
- Introduce the `IExecutor` contract (`ConnectAsync`, `DisconnectAsync`, `MessageAsync`),
  supporting contracts (`ExecutorCapabilities`, connection/disconnection/message contexts,
  progress, result), and `IExecutorRegistry` for resolving executors by key.
- Provide a `MockExecutor` reference implementation of `IExecutor` — configurable
  success/failure/progress/cancellation behavior — so the broker/worker pipeline is usable and
  testable before any real CLI- or gRPC-backed executor is written. Concrete executors
  (`ClaudeCliExecutor`, `GitCliExecutor`, `DotnetCliExecutor`, `LocalGrpcExecutor<TClient>`) are
  explicitly deferred to later changes.
- Introduce broker-side admission and scheduling: a bounded `Channel<ExecutionRequest>` with
  explicit backpressure and user-visible rejection when full.
- Introduce the Slack gateway: Socket Mode connection, request validation, allowlist-based
  authorization (users, executor keys, operations, aliases), and routing of progress/results
  back to the originating Slack thread.
- Introduce worker-side dispatch: `IExecutionDispatcher` resolving executors through the
  registry, forwarding lifecycle/progress/result events through `IExecutionEventSink`, and
  propagating cancellation.
- Add an xUnit test project and test plan covering the protocol, executor framework (via
  `MockExecutor`), scheduler, and dispatcher.

## Capabilities

### New Capabilities
- `execution-protocol`: IPC envelope, message types, and Unix-domain-socket framing between
  broker and worker.
- `executor-framework`: the `IExecutor` contract, supporting types, `IExecutorRegistry`,
  executor lifecycle states, and the `MockExecutor` reference implementation.
- `broker-scheduling`: bounded in-memory admission queue and backpressure policy on the broker.
- `slack-gateway`: Socket Mode connection, request validation/authorization, and Slack thread
  routing for progress and results.
- `worker-dispatch`: the worker-side dispatcher that resolves executors, drives their lifecycle,
  forwards events, and handles cancellation.

### Modified Capabilities
- None — this is a greenfield change; `openspec/specs/` currently has no existing capabilities.

## Impact

- Affected code: net-new. Adds broker and worker project(s) alongside the existing
  `slackbot-broker.csproj`, plus a new xUnit test project.
- Dependencies: Slack Socket Mode client library, `System.Threading.Channels` (BCL), a
  JSON serializer for the IPC envelope, xUnit + test host packages.
- No concrete executor implementations, no real gRPC client integration, and no persistence are
  part of this change — those follow in later changes once this foundation lands.
