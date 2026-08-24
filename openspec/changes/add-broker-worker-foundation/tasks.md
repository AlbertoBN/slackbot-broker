## 1. Solution and Project Scaffolding

- [x] 1.1 Create `slackbot-broker.slnx` (the .NET 10 SDK's default solution format) and add the existing `slackbot-broker.csproj` to it; verify `dotnet build` succeeds at the solution level
- [x] 1.2 Create `src/SlackBotBroker.Protocol` class library (net10.0) and add it to the solution; verify `dotnet build` succeeds
- [x] 1.3 Create `src/SlackBotBroker.Executors` class library, referencing `SlackBotBroker.Protocol`, and add it to the solution; verify `dotnet build` succeeds
- [x] 1.4 Create `src/SlackBotBroker.Worker` executable project, referencing `Protocol` and `Executors`, and add it to the solution; verify `dotnet run --project src/SlackBotBroker.Worker` starts and exits cleanly
- [x] 1.5 Repurpose `slackbot-broker.csproj`/`Program.cs` as the Broker host — relocated to `src/SlackBotBroker.Broker/SlackBotBroker.Broker.csproj` (see design note below), add a reference to `SlackBotBroker.Protocol`, and remove the placeholder "Hello, World!" output; verify `dotnet run` starts and exits cleanly
- [x] 1.6 Add xUnit test projects `tests/SlackBotBroker.Protocol.Tests`, `tests/SlackBotBroker.Executors.Tests`, `tests/SlackBotBroker.Worker.Tests`, `tests/SlackBotBroker.Broker.Tests`, `tests/SlackBotBroker.IntegrationTests`, each referencing its corresponding `src` project(s), and add them to the solution; verify `dotnet test` runs (with zero tests) across all five without error

## 2. Execution Protocol (`execution-protocol`)

- [x] 2.1 Implement the message envelope type (`messageType`, `protocolVersion`, `requestId`, `correlationId`, `sentAtUtc`, `payload`) and verify a round-trip serialize/deserialize unit test passes
- [x] 2.2 Implement `ExecutionRequest` payload (`RequestId`, `CorrelationId`, `SlackChannelId`, `SlackThreadTs`, `RequestedByUserId`, `ExecutorKey`, `Operation`, `PayloadVersion`, `Payload`, `CreatedAtUtc`, optional `TargetAlias`/`Mode`/`TimeoutSeconds`) and verify a unit test asserts all required fields round-trip
- [x] 2.3 Implement `ExecutionAccepted`, `ExecutionProgress`, `ExecutionCompleted`, `ExecutionFailed`, `ExecutionCancelled` payloads, each carrying the originating `RequestId`, and verify unit tests assert correlation is preserved through serialization
- [x] 2.4 Implement `CancelExecution`, `ExecutorStatusRequest`, `ExecutorStatusResponse`, `ConnectExecutor`, `DisconnectExecutor`, `HealthPing`, `HealthPong` payloads and verify unit tests cover each type's round-trip — also added `ConnectExecutorResult`/`DisconnectExecutorResult` payloads (not in the original bullet) since the `execution-protocol` spec's "worker SHALL respond indicating success or failure" requirement needs a wire-representable response distinct from the request; see design.md note
- [x] 2.5 Add a `JsonSerializerContext` source-generated for the envelope and every payload type and verify a unit test confirms serialization does not fall back to reflection (e.g. asserts against the generated context)
- [x] 2.6 Implement newline-delimited framing (write/read one compact JSON line per message over a `Stream`) and verify a unit test sends multiple messages back-to-back on an in-memory stream and confirms each is parsed independently
- [x] 2.7 Add a unit test that a malformed envelope (missing a required field) is rejected without the payload being processed
- [x] 2.8 Implement client-side reconnect-with-backoff behavior for a broken Unix domain socket connection and verify a unit test simulates a dropped connection and confirms reconnection is attempted rather than the process terminating — implemented transport-agnostically (a supplied connect delegate) per design.md so it's unit-testable without a real socket; actual UDS wiring happens in task group 7
- [x] 2.9 Implement `HealthPing`/`HealthPong` liveness exchange and verify a unit test confirms a missed `HealthPong` within the configured window is treated as a broken session

## 3. Executor Framework (`executor-framework`)

- [ ] 3.1 Define the `IExecutor` contract (`ExecutorKey`, `Capabilities`, `ConnectAsync`, `DisconnectAsync`, `MessageAsync`) and supporting types (`ExecutorCapabilities`, `ExecutorConnectionContext`/`Result`, `ExecutorDisconnectContext`, `ExecutorMessageContext`, `ExecutorProgress`, `ExecutorMessageResult`) and verify the project builds with no implementation yet
- [ ] 3.2 Define the executor lifecycle state enum (`Disconnected`, `Connecting`, `Ready`, `Busy`, `Degraded`, `Faulted`, `Disconnecting`) and verify a unit test exercises each transition on a minimal test double
- [ ] 3.3 Implement `IExecutorRegistry` resolving executors by key and rejecting unknown/disabled keys, and verify unit tests cover resolve-known, resolve-unknown, and resolve-disabled cases
- [ ] 3.4 Implement `MockExecutor` with configurable outcomes (success, failure, timeout, cancellation) and a configurable ordered progress sequence, and verify unit tests cover each configured outcome
- [ ] 3.5 Verify a unit test that `MessageAsync` on any executor rejects an operation not declared in `Capabilities` before invoking executor-specific logic
- [ ] 3.6 Verify a unit test that `MessageAsync` respects a supplied timeout and returns a normalized timeout outcome, and another that a triggered `CancellationToken` returns a normalized cancelled outcome, using `MockExecutor`
- [ ] 3.7 Verify a unit test that `ConnectAsync` is idempotent when already `Ready`, and that `DisconnectAsync` is safe after a failed/partial connect and does not terminate an externally-managed executor's underlying process, using `MockExecutor` configured for each case
- [ ] 3.8 Wire an `Executors:Mock:Enabled` configuration flag that gates whether `MockExecutor` is registered in `IExecutorRegistry`, and verify a unit test confirms it is absent from the registry when the flag is unset/false

## 4. Worker Dispatch (`worker-dispatch`)

- [ ] 4.1 Implement `IExecutionDispatcher` and `IExecutionEventSink` and verify the project builds with a test double sink
- [ ] 4.2 Implement executor resolution on `DispatchAsync` (resolve via registry before any other action) and verify a unit test confirms an unregistered executor key produces a failure event without invoking any executor
- [ ] 4.3 Implement admission acknowledgement (`AcceptedAsync` on valid request, `FailedAsync` on invalid request, before execution begins) and verify unit tests cover both paths using `MockExecutor`
- [ ] 4.4 Implement progress and terminal event forwarding from the executor to the event sink, preserving order and correlation, and verify a unit test using `MockExecutor`'s configured progress sequence asserts events arrive in order and the terminal event is emitted exactly once
- [ ] 4.5 Implement cancellation propagation (`CancelExecution` → executor cancellation token → `CancelledAsync`) and verify unit tests cover cancelling an in-flight execution and cancelling an already-terminal execution (no duplicate terminal event)
- [ ] 4.6 Implement sequential execution per target alias (no two in-flight executions against the same target) and verify a unit test with two requests sharing a target alias and `MockExecutor` confirms the second does not start until the first reaches a terminal outcome
- [ ] 4.7 Implement executor status reporting (current lifecycle state and capabilities, independent of any single execution) and verify a unit test asserts the reported state is `Busy` while a message is in flight

## 5. Broker Scheduling (`broker-scheduling`)

- [ ] 5.1 Implement the bounded `Channel<ExecutionRequest>`-backed admission queue with configurable capacity and verify a unit test admits a request while capacity remains
- [ ] 5.2 Implement `TryWrite`-based admission that returns an explicit busy/retry rejection when the queue is full, and verify a unit test fills the queue to capacity and asserts the next submission is rejected and not enqueued
- [ ] 5.3 Verify a unit test that two admitted requests are read from the queue and dispatched in FIFO submission order
- [ ] 5.4 Implement the v1 single-global-execution default (subsequent admitted requests wait for the active one to reach a terminal outcome) and verify a unit test confirms a second request is not forwarded to the worker until the first's terminal outcome is delivered
- [ ] 5.5 Implement removal of a request's active in-memory state once its terminal outcome is delivered and verify a unit test asserts the request is absent from active state after delivery

## 6. Slack Gateway (`slack-gateway`)

- [ ] 6.1 Define a narrow `ISlackClient`-style seam (receive command, send message to channel/thread) and a hand-written fake implementation for tests; verify the project builds against the fake with no real Slack SDK call in tests
- [ ] 6.2 Wire the Socket Mode connection behind the `ISlackClient` seam (no inbound HTTP listener) and verify a unit test using the fake confirms a command is received purely over the seam
- [ ] 6.3 Implement caller authorization against a configured allowlist and verify a unit test confirms an unauthorized user's command is rejected and no `ExecutionRequest` is created
- [ ] 6.4 Implement executor-key and operation allowlist validation and verify unit tests cover an unknown executor key and a disallowed operation, both rejected before dispatch
- [ ] 6.5 Implement alias-only target resolution (reject raw paths/endpoints) and verify a unit test confirms a raw path supplied instead of a configured alias is rejected
- [ ] 6.6 Implement thread-scoped delivery of progress/results using the request's `SlackChannelId`/`SlackThreadTs` and verify a unit test confirms a progress update is posted to the originating thread and not elsewhere
- [ ] 6.7 Implement user-visible failure reporting for queue-full, IPC-unavailable, executor-unavailable, timeout, and cancellation outcomes, each distinguishable, and verify unit tests cover the queue-full and IPC-unavailable cases
- [ ] 6.8 Implement the confirmation workflow for operations flagged high-impact and verify unit tests cover both withholding dispatch pending confirmation and dispatching after explicit confirmation
- [ ] 6.9 Implement configurable sensitive-pattern redaction applied before any content is posted to Slack and verify a unit test confirms a token-shaped pattern in executor output is redacted before delivery

## 7. Host Wiring

- [ ] 7.1 Wire `SlackBotBroker.Worker`'s host to listen on a configured Unix domain socket path, accept the broker's connection, and route messages to `IExecutionDispatcher`; verify manual run: start the worker and confirm it listens on the configured path
- [ ] 7.2 Wire the Broker host to connect to the worker's Unix domain socket, submit admitted requests, and forward worker events to `ISlackClient`; verify manual run: start worker then broker and confirm the IPC connection is established (e.g. via a successful `HealthPing`/`HealthPong` exchange)
- [ ] 7.3 Wire `Executors:Mock:Enabled` through worker configuration so a local/dev run can exercise the full path with `MockExecutor`; verify manual run: submit a fake Slack command end-to-end and observe an `ExecutionCompleted` event reach the broker

## 8. Integration Tests

- [ ] 8.1 In `SlackBotBroker.IntegrationTests`, start a real worker host and broker-side IPC client over a temp-path Unix domain socket and verify a `HealthPing`/`HealthPong` round-trip succeeds
- [ ] 8.2 Verify an end-to-end integration test: a fake Slack command drives the broker's scheduler → IPC → worker dispatcher → `MockExecutor` (configured for success) → back through IPC → and asserts a completed result reaches the fake `ISlackClient` in the originating thread
- [ ] 8.3 Verify an end-to-end integration test covering cancellation: submit a request against a `MockExecutor` configured to hang, send a cancel, and assert a cancelled result reaches the fake `ISlackClient`
- [ ] 8.4 Verify an end-to-end integration test covering queue-full: fill the broker's bounded queue and assert the next Slack command receives a busy/retry response without reaching the worker
- [ ] 8.5 Verify an end-to-end integration test covering worker unavailability: stop the worker process/listener and assert the broker surfaces an IPC-unavailable message rather than hanging or crashing

## 9. Final Verification

- [ ] 9.1 Run `dotnet test` across the full solution and verify all tests pass
- [ ] 9.2 Run `openspec validate add-broker-worker-foundation --strict` and verify it reports no errors
