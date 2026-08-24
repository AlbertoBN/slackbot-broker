# Product Requirements Document: Slack-Controlled Local Application Broker

## Overview

This product defines a local-control system in which Slack is the primary operator interface for a development machine. A Slack app connects to a .NET 10 broker using Socket Mode. The broker validates Slack requests, routes approved work to a separate local worker over a bidirectional IPC protocol, and forwards progress and results back to the originating Slack thread. Socket Mode lets Slack deliver Events API and interactive payloads over WebSockets without requiring a public inbound HTTP endpoint. [cite:1]

The broker is not limited to Claude Code or other CLIs. It controls a bounded, approved set of local applications through a common `IExecutor` abstraction. Concrete executors encapsulate transport and application-specific behavior: CLI executors start and supervise local processes; gRPC executors connect to locally running gRPC services and invoke approved operations through generated clients. A .NET gRPC client is created from a long-lived channel, which is the appropriate resource boundary for a gRPC-backed executor. [cite:53]

The system intentionally avoids RabbitMQ and distributed queue infrastructure in v1. It targets one development machine, direct request/response semantics, low operational complexity, and a controlled local application boundary.

## Goals

- Provide a secure Slack control surface for approved applications running on the local development machine.
- Receive Slack commands through Socket Mode without exposing a public inbound HTTP endpoint. [cite:1]
- Isolate application execution from the Slack-facing broker in a separate local worker process.
- Support CLI-backed and gRPC-backed applications through a common `IExecutor` contract.
- Support request acceptance, progress updates, final results, errors, health, and cancellation through a bidirectional local protocol.
- Use bounded in-memory scheduling inside the broker to provide explicit backpressure. `System.Threading.Channels` supplies asynchronous producer/consumer primitives for this role. [cite:33]
- Keep v1 single-host and simple enough to operate locally.

## Non-Goals

- Distributed execution across machines.
- Durable job persistence, replay, or offline processing in v1.
- Arbitrary shell or arbitrary gRPC method invocation from Slack.
- Generic remote desktop or unrestricted machine administration.
- Automatic discovery and control of every application on the machine.
- Horizontal worker scaling.

## Users and Core Use Cases

The primary user is a trusted operator using Slack as the main interface to the local development machine. The system supports:

- Running approved CLI operations against preapproved repositories or tools.
- Calling approved operations exposed by local gRPC applications.
- Asking Claude Code to analyze, plan, apply, test, or summarize work in a repo scoped by alias.
- Invoking other local development tools through a defined executor contract.
- Receiving operation progress and final output in the originating Slack thread.
- Cancelling an in-progress operation when its executor supports cooperative cancellation.
- Querying status and health for configured local applications.

## Architecture

### Components

| Component | Responsibility |
|---|---|
| Slack App | Receives slash commands, app mentions, and interactive actions through Socket Mode. [cite:1] |
| Slack Broker (.NET 10) | Maintains the Slack connection, validates requests, applies authorization and policy, schedules work, communicates with the local worker, and posts Slack updates. |
| Broker Scheduler | Uses a bounded `Channel<ExecutionRequest>` to buffer and dispatch local work with backpressure. [cite:33] |
| Local Worker Process | Owns executor instances, dispatches requests, supervises running operations, and emits structured outcomes. |
| IPC Transport | Maintains a persistent bidirectional connection between broker and worker, using a Unix domain socket on Ubuntu/Linux in v1. |
| Executor Registry | Resolves an approved executor implementation by executor key and validates that requested operations are allowed. |
| Local Applications | Approved CLI programs, local gRPC services, and future supported application adapters. |

### Selected Topology

```text
Slack
  │ Socket Mode / WebSocket
  ▼
Slack Broker (.NET 10)
  │
  ├─ Request validation, authorization, Slack thread routing
  ├─ Bounded in-memory Channel<ExecutionRequest>
  │
  │ Persistent two-way local IPC
  ▼
Local Worker (.NET 10)
  │
  ├─ Executor Registry
  ├─ IExecutor implementations
  │    ├─ ClaudeCliExecutor
  │    ├─ GitCliExecutor
  │    ├─ DotnetCliExecutor
  │    ├─ LocalGrpcExecutor<TClient>
  │    └─ Future approved executors
  ▼
Approved local applications
```

The broker and worker are separate processes so a crash, hang, or restart in an executor host does not directly terminate the Slack connection. The broker’s internal channel is an in-process producer/consumer queue and must be bounded; channels provide asynchronous producer/consumer coordination and FIFO handoff. [cite:33]

## Executor Model

### `IExecutor`

`ICliExecutor` is replaced by `IExecutor`. The interface represents a controlled adapter to one configured local application, rather than a raw terminal abstraction.

The broker never decides how a particular application launches, connects, serializes requests, interprets output, or shuts down. Those details remain inside the relevant executor implementation.

A conceptual interface is:

```csharp
public interface IExecutor
{
    string ExecutorKey { get; }
    ExecutorCapabilities Capabilities { get; }

    Task<ExecutorConnectionResult> ConnectAsync(
        ExecutorConnectionContext context,
        CancellationToken cancellationToken);

    Task DisconnectAsync(
        ExecutorDisconnectContext context,
        CancellationToken cancellationToken);

    Task<ExecutorMessageResult> MessageAsync(
        ExecutorMessageContext context,
        IProgress<ExecutorProgress>? progress,
        CancellationToken cancellationToken);
}
```

### Contract Semantics

| Method | Purpose | Required behavior |
|---|---|---|
| `ConnectAsync` | Establish or validate executor readiness | Must be idempotent where feasible. It may start a managed process, test a CLI dependency, open a gRPC channel, or verify a service health endpoint. |
| `DisconnectAsync` | Release resources or stop a managed connection | Must be safe to call after a failed or partial connect. It must not terminate an externally managed app unless policy explicitly allows that. |
| `MessageAsync` | Deliver one approved structured request to the application | Must validate the operation, honor cancellation and timeout, emit progress where available, and return a normalized structured result. |

`MessageAsync` is intentionally generic at the common interface level. The message payload must be a typed or versioned application contract, never arbitrary free-form shell text or an arbitrary gRPC method name.

### Supporting Contracts

The common model should include:

- `ExecutorCapabilities`: supports connect, disconnect, cancellation, progress, streaming, concurrent messages, managed lifecycle, and health checks.
- `ExecutorConnectionContext`: request identity, environment/profile, caller identity, and executor-specific approved configuration reference.
- `ExecutorDisconnectContext`: disconnect reason and lifecycle policy.
- `ExecutorMessageContext`: request metadata, operation name, typed/versioned payload, repo alias or target alias if relevant, timeout, and cancellation information.
- `ExecutorProgress`: normalized status, display message, stage, percent if meaningful, timestamp, and optional safe detail.
- `ExecutorMessageResult`: success/failure status, user-safe summary, technical detail, structured output, artifacts, exit code or transport status, and duration.

### Executor Implementations

| Executor | Application type | `ConnectAsync` behavior | `MessageAsync` behavior |
|---|---|---|---|
| `ClaudeCliExecutor` | CLI | Verify `claude` availability and approved repo context; no persistent process is required unless later desired | Build safe arguments, start Claude Code in the approved working directory, capture output, map result to normalized response |
| `GitCliExecutor` | CLI | Verify Git availability and repository access | Run allowlisted Git operations only, never raw arbitrary command text |
| `DotnetCliExecutor` | CLI | Verify `dotnet` SDK availability and configured workspace | Run allowlisted build/test/format operations with controlled arguments |
| `LocalGrpcExecutor<TClient>` | Local gRPC service | Create and retain a gRPC channel/client, validate service readiness | Invoke only registered client operations and map response/streaming events to normalized progress and result |
| Future application executor | CLI, gRPC, or another approved local protocol | Encapsulate application-specific initialization | Encapsulate application-specific messaging and result normalization |

For gRPC executors, the implementation should create a long-lived `GrpcChannel` and use it to create the generated client. .NET guidance describes the gRPC channel as a long-lived connection to a service, and it supports unary as well as streaming calls. [cite:53]

### Lifecycle Policy

The worker owns executor lifecycle. The broker requests actions but does not directly launch processes or create gRPC clients.

- An executor can be **stateless**: each `MessageAsync` starts and awaits a local CLI process.
- An executor can be **connection-oriented**: `ConnectAsync` establishes a reusable gRPC channel or an application session.
- An executor can be **managed**: it may start/stop a locally managed application only if explicit executor policy permits it.
- An executor can be **externally managed**: `DisconnectAsync` releases the adapter’s resources but never stops the underlying app.

The worker should expose executor status separately from a single request result: `Disconnected`, `Connecting`, `Ready`, `Busy`, `Degraded`, `Faulted`, and `Disconnecting`.

## Object-Oriented Design

### Patterns

| Pattern | Use |
|---|---|
| Strategy | Each `IExecutor` implementation owns the behavior for a distinct local application or protocol. |
| Factory/Registry | `IExecutorRegistry` resolves a configured executor by `ExecutorKey`; it rejects unknown or disabled executors. |
| Command | Each inbound operation is represented as an `ExecutionRequest` containing a declared executor key, operation, and typed payload. |
| Adapter | Executors adapt raw CLI process APIs, generated gRPC clients, or future local protocols to the common `IExecutor` contract. |
| State | Executor lifecycle and active operation state are represented explicitly for status reporting and safe transitions. |
| Mediator/Dispatcher | `ExecutionDispatcher` coordinates policy validation, executor resolution, operation lifecycle, progress forwarding, and cancellation without coupling the IPC transport to individual executors. |

### Core Interfaces

```csharp
public interface IExecutorRegistry
{
    bool TryGet(string executorKey, out IExecutor executor);
    IReadOnlyCollection<ExecutorDescriptor> GetAvailableExecutors();
}

public interface IExecutionDispatcher
{
    Task DispatchAsync(
        ExecutionRequest request,
        IExecutionEventSink eventSink,
        CancellationToken cancellationToken);
}

public interface IExecutionEventSink
{
    Task AcceptedAsync(ExecutionAccepted accepted, CancellationToken cancellationToken);
    Task ProgressAsync(ExecutionProgress progress, CancellationToken cancellationToken);
    Task CompletedAsync(ExecutionCompleted completed, CancellationToken cancellationToken);
    Task FailedAsync(ExecutionFailed failed, CancellationToken cancellationToken);
    Task CancelledAsync(ExecutionCancelled cancelled, CancellationToken cancellationToken);
}
```

## Protocol Design

### IPC Transport

The v1 transport is a persistent Unix domain socket connection between broker and worker on Ubuntu/Linux. The protocol remains transport-agnostic so a future implementation can use named pipes or gRPC over local IPC without changing the application-level message contracts.

The transport carries framed UTF-8 JSON envelopes. NDJSON is acceptable for v1 if messages are bounded and framing rules are deterministic.

### Envelope

Every protocol message includes:

- `messageType`
- `protocolVersion`
- `requestId`
- `correlationId`
- `sentAtUtc`
- `payload`

### Core Messages

| Message type | Direction | Purpose |
|---|---|---|
| `ExecutionRequest` | Broker -> Worker | Request an operation from a declared executor |
| `ExecutionAccepted` | Worker -> Broker | Confirm admission to worker scheduling |
| `ExecutionProgress` | Worker -> Broker | Report normalized progress |
| `ExecutionCompleted` | Worker -> Broker | Report successful normalized result |
| `ExecutionFailed` | Worker -> Broker | Report structured terminal failure |
| `ExecutionCancelled` | Worker -> Broker | Confirm cancellation |
| `CancelExecution` | Broker -> Worker | Request cancellation by request ID |
| `ExecutorStatusRequest` | Broker -> Worker | Ask for configured executor status |
| `ExecutorStatusResponse` | Worker -> Broker | Return executor state and capabilities |
| `ConnectExecutor` | Broker -> Worker | Request connection/readiness for an approved executor |
| `DisconnectExecutor` | Broker -> Worker | Request adapter disconnect subject to policy |
| `HealthPing` / `HealthPong` | Both directions | Maintain liveness and diagnose a broken IPC session |

### `ExecutionRequest`

An execution request must contain:

- `RequestId`
- `CorrelationId`
- `SlackChannelId`
- `SlackThreadTs`
- `RequestedByUserId`
- `ExecutorKey`
- `Operation`
- `PayloadVersion`
- `Payload`
- `TargetAlias` when relevant, such as a repository alias or configured local-service alias
- `Mode`, such as `ReadOnly`, `Plan`, or `Apply`
- `TimeoutSeconds`
- `CreatedAtUtc`

An `ExecutorKey` identifies the approved adapter, for example `claude-code`, `git`, `dotnet`, or `openspec-service`. The `Operation` is validated against that executor’s registered capabilities. The payload is executor-defined but must conform to a versioned contract known to the implementation.

## Request Lifecycle

1. A user invokes an approved Slack command.
2. Slack sends the event to the broker over Socket Mode. [cite:1]
3. The broker validates Slack identity, command structure, executor key, target alias, mode, and policy.
4. The broker creates an `ExecutionRequest`, records Slack routing context, and writes it to a bounded in-memory channel. Channels provide asynchronous producer/consumer handoff and bounded channels can apply backpressure when capacity is exhausted. [cite:33]
5. The dispatcher forwards the request to the worker over the persistent IPC session.
6. The worker validates executor availability and operation policy, then emits `ExecutionAccepted` or `ExecutionFailed`.
7. The dispatcher resolves the correct `IExecutor` using `IExecutorRegistry`.
8. The executor connects if necessary, invokes `MessageAsync`, and emits progress through the worker event sink.
9. The worker emits `ExecutionCompleted`, `ExecutionFailed`, or `ExecutionCancelled`.
10. The broker forwards safe progress and final content into the originating Slack thread.
11. The broker removes active in-memory state once a terminal outcome is delivered or retained observability data is recorded.

## Scheduling and Concurrency

The broker shall use a bounded `Channel<ExecutionRequest>` for admission and scheduling. `System.Threading.Channels` provides asynchronous producer/consumer data structures, with writers and readers used for asynchronous handoff. [cite:33]

V1 defaults:

- One active execution globally.
- A small bounded queue, configurable by policy.
- Explicit user-visible rejection when queue capacity is exhausted.
- Per-executor concurrency metadata for later expansion.
- Sequential execution for tools that operate on the same repository or mutate the same local state.

Future policy can allow concurrency by executor or target alias only where the executor declares it safe.

## Security and Policy

The broadening from CLI-only to multiple local applications increases the security importance of the executor boundary. The system shall treat an executor as a permissioned adapter, not a generic transport proxy.

- Only allowlisted Slack users or roles may invoke operations.
- Only configured executor keys may be used.
- Each executor declares an explicit allowlist of operations.
- Repo paths and local service endpoints are selected through configured aliases, never raw values supplied by Slack.
- CLI implementations use explicit process arguments and approved working directories; they must not concatenate arbitrary shell strings.
- gRPC implementations invoke only generated client methods that are explicitly registered by the executor; they must not accept an arbitrary service/method name from Slack.
- High-impact actions such as edit/apply, process lifecycle management, source-control mutation, or destructive service calls require a Slack confirmation workflow.
- Secrets, tokens, environment variables, and raw sensitive output must be redacted before Slack delivery and structured logging.
- The worker process runs with least privilege practical for the local development workload.

## Error Handling

The system shall normalize errors into user-safe and diagnostic forms.

| Failure class | Broker behavior | Worker/executor behavior |
|---|---|---|
| Validation failure | Reject immediately in Slack | No worker dispatch |
| Queue full | Return clear busy/retry message | No worker dispatch |
| IPC unavailable | Return local-worker-unavailable message | Reconnect with bounded backoff |
| Executor unavailable | Report executor status or connection failure | Return structured readiness failure |
| Unsupported operation | Reject before invocation | Never attempt raw fallback |
| CLI non-zero exit | Return concise summary and safe diagnostic snippet | Preserve exit code and normalized technical detail |
| gRPC transport/RPC failure | Return service-unavailable or operation-failed status | Capture gRPC status and retryability classification |
| Timeout | Report timeout and issue cancellation | Terminate/cooperatively cancel according to executor policy |
| User cancellation | Confirm cancellation request and final state | Propagate `CancellationToken` and report terminal result |

## Observability

Broker and worker logs must include:

- `RequestId`
- `CorrelationId`
- Slack channel and thread identifiers
- Caller identity
- `ExecutorKey`
- `Operation`
- target alias
- lifecycle state
- start time, completion time, and duration
- outcome and normalized failure category

Operational metrics should include:

- broker internal channel depth
- queue rejection count
- worker IPC connectivity
- executor readiness by executor key
- active executions
- duration and outcome by executor/operation
- cancellation count
- CLI exit-code distribution
- gRPC status-code distribution

## Configuration

Configuration shall include:

- Slack credentials and Socket Mode app-level token. [cite:1]
- Authorized users/roles.
- Internal channel capacity and full-mode policy.
- IPC endpoint path and connection settings.
- Executor registry configuration.
- Per-executor enabled state, target aliases, allowed operations, concurrency limit, timeout, and lifecycle policy.
- Repository alias mappings for CLI executors.
- Local gRPC endpoint aliases and transport-security configuration for gRPC executors.
- Output truncation/redaction policies.
- Logging and health-check configuration.

## Acceptance Criteria

The v1 system is accepted when:

- The broker receives and responds to an approved Slack request through Socket Mode. [cite:1]
- A validated request is placed onto the broker’s bounded internal channel and dispatched to the worker. [cite:33]
- The worker resolves a configured `IExecutor` by executor key.
- At least one CLI implementation, `ClaudeCliExecutor`, completes a repo-aliased request and returns a normalized result.
- At least one gRPC implementation connects to a configured local service and invokes one explicitly allowed operation using a generated client. A .NET gRPC client is created from a `GrpcChannel`, which represents a long-lived service connection. [cite:53]
- Progress and final outcomes reach the originating Slack thread.
- Unauthorized executors, operations, aliases, and raw paths are rejected before execution.
- Queue-full, IPC-unavailable, executor-unavailable, timeout, and cancellation outcomes are visible to the user and structured in logs.
- `DisconnectAsync` releases executor resources safely and does not terminate externally managed applications unless configuration explicitly permits it.

## Recommended Initial Scope

V1 should start with:

- One Slack command surface.
- One broker and one worker process.
- Unix domain socket IPC.
- One globally active execution.
- A bounded in-memory request channel.
- `ClaudeCliExecutor` for the initial Claude Code workflow.
- One `LocalGrpcExecutor<TClient>` proof-of-concept for a single approved local gRPC application.
- Basic `ConnectAsync`, `DisconnectAsync`, `MessageAsync`, status, progress, cancellation, logging, and Slack thread updates.

This establishes a stable executor architecture while keeping the initial deployment local and operationally lightweight.
