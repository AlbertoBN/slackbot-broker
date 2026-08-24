# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project status

This repository is early-stage. Solution/project scaffolding exists (see Commands and Solution
layout below) but almost no behavior is implemented yet — see the in-progress OpenSpec change
`add-broker-worker-foundation` for what's built vs. pending. The real design lives in
[docs/slack-broker-prd-v2.md](docs/slack-broker-prd-v2.md) — read it before making
architectural decisions not yet covered by an OpenSpec change.

## Commands

- Build: `dotnet build` (builds the whole solution, `slackbot-broker.slnx`)
- Test: `dotnet test`
- Run broker: `dotnet run --project src/SlackBotBroker.Broker`
- Run worker: `dotnet run --project src/SlackBotBroker.Worker`
- Restore: `dotnet restore`
- Run a single test project: `dotnet test tests/SlackBotBroker.Protocol.Tests` (swap the project
  name for `Executors`, `Worker`, `Broker`, or `IntegrationTests`)

Target framework is `net10.0` throughout. No linter/formatter is configured yet.

## Solution layout

Every project lives in its own subdirectory under `src/` or `tests/` — none is nested inside
another project's directory. (The original root-level `slackbot-broker.csproj` placeholder was
relocated to `src/SlackBotBroker.Broker/` for exactly this reason: an SDK-style project's default
compile-item glob only excludes its own top-level `bin/`/`obj/`, not a subdirectory's, so a
project sitting at the repo root with sibling projects nested under it would glob in their
generated files too and fail to build.)

- `src/SlackBotBroker.Protocol` — IPC envelope, message types, JSON (de)serialization, UDS framing.
- `src/SlackBotBroker.Executors` — `IExecutor` contract, `IExecutorRegistry`, `MockExecutor`.
- `src/SlackBotBroker.Worker` — worker host (executable).
- `src/SlackBotBroker.Broker` — broker host (executable); the original placeholder project.
- `tests/SlackBotBroker.{Protocol,Executors,Worker,Broker}.Tests` — xUnit, one per `src` project.
- `tests/SlackBotBroker.IntegrationTests` — xUnit, end-to-end broker+worker over a real Unix
  domain socket, driven through `MockExecutor`.

## Spec-driven workflow (OpenSpec)

This repo uses OpenSpec (`openspec/`) to manage change proposals and specs before
implementation. Corresponding slash commands are available under `.claude/commands/opsx/`:
`/opsx:propose`, `/opsx:explore`, `/opsx:apply`, `/opsx:sync`, `/opsx:archive`. Prefer proposing
an OpenSpec change before implementing non-trivial functionality, rather than editing code
directly. Check `openspec/changes/` for in-progress changes and their `tasks.md` before assuming
something is unimplemented.

## Architecture (per the PRD — target design, not yet implemented)

The system is a Slack-controlled broker for running approved local applications (CLIs, local
gRPC services) on a single development machine, without RabbitMQ or any distributed queue.

**Two-process design:**
- **Slack Broker** (.NET 10) — holds the Slack Socket Mode connection (no public inbound HTTP
  endpoint), validates/authorizes requests, schedules work on a bounded
  `Channel<ExecutionRequest>` for backpressure, and posts progress/results back to the
  originating Slack thread.
- **Local Worker** (.NET 10) — a separate process that owns executor instances, dispatches
  requests, supervises running operations, and emits structured outcomes. Kept separate so a
  crash/hang in an executor doesn't take down the Slack connection.

They communicate over a persistent, bidirectional **Unix domain socket** IPC transport carrying
framed UTF-8 JSON (NDJSON acceptable) envelopes with fields: `messageType`, `protocolVersion`,
`requestId`, `correlationId`, `sentAtUtc`, `payload`. Core message types: `ExecutionRequest`,
`ExecutionAccepted`, `ExecutionProgress`, `ExecutionCompleted`, `ExecutionFailed`,
`ExecutionCancelled`, `CancelExecution`, `ExecutorStatusRequest`/`Response`,
`ConnectExecutor`/`DisconnectExecutor`, `HealthPing`/`HealthPong`.

**Executor model** — the central abstraction. All local applications are controlled through a
common `IExecutor` interface (`ConnectAsync`, `DisconnectAsync`, `MessageAsync`), resolved by
key via `IExecutorRegistry`. The broker/worker never launch processes or invoke arbitrary
commands directly — everything goes through an executor:
- `ClaudeCliExecutor`, `GitCliExecutor`, `DotnetCliExecutor` — CLI-backed, run allowlisted
  operations with explicit arguments (never raw shell strings).
- `LocalGrpcExecutor<TClient>` — gRPC-backed, holds a long-lived `GrpcChannel`/generated client,
  invokes only explicitly registered client methods (never an arbitrary service/method name).

Executors are Strategy implementations; `IExecutorRegistry` is a Factory/Registry;
`ExecutionRequest` is a Command; `ExecutionDispatcher` is a Mediator that coordinates policy
validation, executor resolution, lifecycle, progress forwarding, and cancellation without
coupling IPC transport to individual executors. Executor lifecycle states:
`Disconnected`, `Connecting`, `Ready`, `Busy`, `Degraded`, `Faulted`, `Disconnecting`.

**Security boundary** (a core design constraint, not an afterthought):
- Only allowlisted Slack users/roles, configured executor keys, and each executor's declared
  operation allowlist may be invoked.
- Repo paths and service endpoints are selected via configured *aliases*, never raw values from
  Slack.
- High-impact actions (edit/apply, process lifecycle, source-control mutation, destructive gRPC
  calls) require a Slack confirmation step.
- Secrets/tokens/sensitive output must be redacted before reaching Slack or logs.

**Concurrency (v1 defaults):** one active execution globally, small bounded queue, explicit
rejection when full, sequential execution for tools touching the same repo/local state.

See the PRD for full request-lifecycle steps, error-handling matrix, observability fields, and
configuration surface.
