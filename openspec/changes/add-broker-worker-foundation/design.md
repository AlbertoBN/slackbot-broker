## Context

See [proposal.md](proposal.md) - Why for motivation. Today the repo has only a placeholder
`slackbot-broker.csproj` / `Program.cs`. The target architecture (two processes, Unix domain
socket IPC, executor abstraction) is fully described in
[docs/slack-broker-prd-v2.md](../../../docs/slack-broker-prd-v2.md); this design translates that
into a concrete project layout and build order for the five capabilities in this change:
`execution-protocol`, `executor-framework`, `broker-scheduling`, `slack-gateway`, and
`worker-dispatch`. Per the proposal, no concrete `IExecutor` implementation (Claude CLI, Git CLI,
gRPC, ...) is built here — a `MockExecutor` fills that role for now.

## Goals / Non-Goals

**Goals:**
- Stand up broker and worker as separate, independently runnable .NET 10 processes communicating
  over a real Unix domain socket, using the protocol defined in `execution-protocol`.
- Make the full request pipeline (Slack gateway → scheduler → IPC → dispatcher → executor →
  IPC → Slack thread) exercisable and testable using only `MockExecutor`.
- Establish the solution/project layout and xUnit test project(s) that later changes (concrete
  executors, persistence, observability) build on without restructuring.

**Non-Goals:**
- Implementing any concrete `IExecutor` (`ClaudeCliExecutor`, `GitCliExecutor`,
  `DotnetCliExecutor`, `LocalGrpcExecutor<TClient>`) — later changes.
- Structured logging/metrics infrastructure beyond what's needed to observe behavior in tests
  (the PRD's Observability section is not one of this change's capabilities).
- Durable/persisted queue state, multi-host deployment, or per-executor concurrency policy beyond
  the v1 single-global-execution default.

## Decisions

### Solution and project layout
Repurpose the existing `slackbot-broker.csproj`/`Program.cs` placeholder as the **Broker** host
project (it currently does nothing meaningful, so there's no value in leaving it untouched
alongside a differently-named entry point), but relocate it under `src/` alongside the other
projects rather than leaving it at the repo root. Add:

- `src/SlackBotBroker.Protocol` — envelope, message types, JSON (de)serialization, UDS framing.
  No dependency on any other project.
- `src/SlackBotBroker.Executors` — `IExecutor` and supporting contracts, `IExecutorRegistry`,
  `MockExecutor`. Depends only on `Protocol` (for shared types like correlation IDs) if needed.
- `src/SlackBotBroker.Worker` — worker host: dispatcher, executor registry wiring, IPC server
  side. Depends on `Protocol` and `Executors`.
- `src/SlackBotBroker.Broker` (repurposed from the root `slackbot-broker.csproj`) — broker host:
  Slack gateway, scheduler, IPC client side. Depends on `Protocol`.
- `tests/SlackBotBroker.Protocol.Tests`, `tests/SlackBotBroker.Executors.Tests`,
  `tests/SlackBotBroker.Broker.Tests`, `tests/SlackBotBroker.Worker.Tests` — xUnit, one per
  library, matching the capability that owns the behavior.
- `tests/SlackBotBroker.IntegrationTests` — drives a real broker+worker pair over a loopback Unix
  domain socket, end-to-end through `MockExecutor`, without touching real Slack.
- `slackbot-broker.slnx` — ties all projects together for `dotnet build`/`dotnet test` at the
  solution level (the .NET 10 SDK's default solution format).

Alternative considered: keep everything in one project. Rejected — it would blur the boundary
between `execution-protocol`, `executor-framework`, `broker-scheduling`, `slack-gateway`, and
`worker-dispatch` that the specs deliberately separate, and would force broker-only code to
compile into the worker binary and vice versa.

Alternative considered: leave the Broker host's `.csproj` at the repo root with the other
projects nested under `src/`. Rejected during implementation of task 1.1–1.5 — it doesn't build.
An SDK-style project's default compile-item glob (`**/*.cs`) only excludes its own top-level
`bin/`/`obj/` (the SDK's `DefaultExcludesInProjectFolder` is `bin/**;obj/**`, not `**/bin/**`),
so a project sitting at the repo root also globs in any subdirectory's source and generated
`obj/*.AssemblyInfo.cs` files — including a sibling project's — producing duplicate-attribute
compile errors. Every project, including the Broker host, must live in its own subdirectory with
no other project nested inside it.

### IPC framing: newline-delimited JSON over Unix domain socket
Each envelope is serialized as a single line of compact (non-indented) UTF-8 JSON terminated by
`\n`; the writer never emits an embedded raw newline inside a frame because
`System.Text.Json`'s default (non-indented) output never inserts one. This satisfies the PRD's
"NDJSON is acceptable ... if messages are bounded and framing rules are deterministic."

Alternative considered: length-prefixed binary framing. More robust against pathological payload
content, but adds complexity that isn't needed while payloads are small, versioned JSON contracts
under the executor's control (never arbitrary free text). Revisit if a future executor needs to
stream large binary artifacts.

### Serialization
Use `System.Text.Json` with a source-generated `JsonSerializerContext` covering the envelope and
every message payload type, rather than reflection-based serialization. Keeps (de)serialization
fast and AOT/trim-friendly, and forces every message type to be explicitly declared in one place.
The envelope's own fields (`messageType`, `protocolVersion`, ...) are camelCase on the wire via
explicit `[JsonPropertyName]` attributes; every payload type's fields stay PascalCase (the
serializer's default for as-declared property names), matching the field casing already used in
the `execution-protocol` spec and the PRD's `ExecutionRequest` field list.

### Connect/Disconnect executor responses
`ConnectExecutor` and `DisconnectExecutor` are documented in the PRD as one-directional
(broker → worker) message types, but the `execution-protocol` spec requires "the worker SHALL
respond indicating success or failure" for each. Added `ConnectExecutorResult` and
`DisconnectExecutorResult` (worker → broker) message types to carry that response — this wasn't
explicitly called out as a separate bullet in tasks.md task 2.4, which just listed the PRD's
one-directional names; the two response payloads are the minimal, direct fix for that gap, not
new scope beyond what the spec already committed to.

### Scheduling primitive
Use `System.Threading.Channels.Channel.CreateBounded<ExecutionRequest>` with
`FullMode = Wait`, but the broker calls `TryWrite` (not `WriteAsync`) on admission. `TryWrite`
returns `false` immediately when the channel is full, which is what turns into the
"Explicit Backpressure" requirement's busy/retry response — the broker never blocks a Slack
event handler waiting for queue space.

### MockExecutor placement and safety
`MockExecutor` lives in the same `SlackBotBroker.Executors` project as the real contracts (it's
the reference implementation the specs require, not test-only scaffolding), but the worker only
registers it in `IExecutorRegistry` when an explicit `Executors:Mock:Enabled` configuration flag
is set. This keeps it available for local runs, demos, and integration tests without it being
reachable from a default/production-shaped configuration.

### Test doubles over mocking libraries
For `slack-gateway` tests, define a small `ISlackClient`-style seam and a hand-written fake
implementation, rather than adding a mocking library (e.g., Moq). The seam is narrow (send
message to channel/thread, receive command) and a hand-written fake keeps the test project
dependency-light; revisit only if test setups become unwieldy.

### Slack gateway composition
`SlackGateway` depends directly on the concrete `ExecutionScheduler` (both already live in
`SlackBotBroker.Broker`) rather than a further abstraction — `TryAdmit` returning `false` is
exactly the broker-scheduling-defined queue-full signal the gateway needs to surface. It gains
two more narrow seams: `IWorkerConnectionState` (`bool IsConnected`) so "IPC unavailable" is
distinguishable from "queue full" without real IPC existing yet, and `IWorkerEventListener`
(`AcceptedAsync`/`ProgressAsync`/`CompletedAsync`/`FailedAsync`/`CancelledAsync`) — the
broker-side mirror of the worker's `IExecutionEventSink` — so `SlackGateway` can be handed
lifecycle events and route them to the right channel/thread via an internal `RequestId` → route
map populated at admission. Both seams get real implementations wired to the actual IPC client
in host wiring (task group 7); until then, tests drive them directly.

A high-impact command's own future `ExecutionRequestPayload.RequestId` (generated up front, at
validation time) doubles as its confirmation id — the user replies referencing that id, and on
confirmation it becomes the real request's id. This avoids a second token/correlation concept.

## Risks / Trade-offs

- [Risk] Single global execution (per `broker-scheduling`) limits throughput once real executors
  land → Mitigation: the registry/dispatcher already key state by request and executor, so
  raising concurrency later is a scheduling-policy change, not a rework of `worker-dispatch` or
  `executor-framework`.
- [Risk] NDJSON framing is fragile if a payload ever needs embedded newlines or raw binary →
  Mitigation: enforced compact serialization plus a framing test in
  `SlackBotBroker.Protocol.Tests` that rejects/guards against embedded `\n`; revisit framing if a
  future executor needs to stream binary artifacts.
- [Risk] `MockExecutor` accidentally reachable in a real deployment → Mitigation: opt-in
  configuration flag, off unless explicitly set; covered by an `executor-framework`/worker test
  asserting it is absent from the registry when the flag is unset.
- [Risk] Five capabilities landing in one change is a lot of surface → Mitigation: scope is
  intentionally the PRD's "Recommended Initial Scope" minus concrete executors; `tasks.md`
  sequences work so each capability is independently testable via `MockExecutor` and fakes before
  the next depends on it.

## Migration Plan

Greenfield — no existing deployment to migrate. Build order follows the dependency chain:
`execution-protocol` → `executor-framework` (with `MockExecutor`) → `worker-dispatch` →
`broker-scheduling` → `slack-gateway` → wire both hosts (`SlackBotBroker.Worker`,
`slackbot-broker`) together over a real Unix domain socket → integration tests. Each step lands
with its own xUnit coverage before the next step depends on it, so the pipeline is demonstrable
end-to-end (via `MockExecutor`) as soon as `worker-dispatch` and `broker-scheduling` are wired,
even before `slack-gateway` replaces the test harness's fake Slack client with the real Socket
Mode connection.

## Open Questions

- Which Slack Socket Mode client library to use is not decided here — the `slack-gateway` spec
  is written against observable behavior, not a specific SDK, so this can be picked during
  implementation without changing the spec, design, or task breakdown.
- Structured logging/metrics library choice (e.g., Serilog, OpenTelemetry) is deferred to a
  future change that turns the PRD's Observability section into its own capability.
