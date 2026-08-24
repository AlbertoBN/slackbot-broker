## Purpose

Defines the message envelope and message set exchanged between the broker and the worker over
the persistent local IPC transport, so both processes can reliably request work, report status,
and coordinate cancellation.

## ADDED Requirements

### Requirement: Message Envelope
Every protocol message SHALL include `messageType`, `protocolVersion`, `requestId`,
`correlationId`, `sentAtUtc`, and `payload`.

#### Scenario: Well-formed envelope accepted
- **WHEN** a peer sends a JSON message containing all envelope fields
- **THEN** the receiving side parses it and dispatches the payload based on `messageType`

#### Scenario: Malformed envelope rejected
- **WHEN** a message is missing a required envelope field
- **THEN** the receiving side rejects the message and does not process its payload

### Requirement: Execution Request Message
An `ExecutionRequest` sent from broker to worker SHALL contain `RequestId`, `CorrelationId`,
`SlackChannelId`, `SlackThreadTs`, `RequestedByUserId`, `ExecutorKey`, `Operation`,
`PayloadVersion`, `Payload`, `CreatedAtUtc`, and, when relevant, `TargetAlias`, `Mode`, and
`TimeoutSeconds`.

#### Scenario: Broker sends a complete execution request
- **WHEN** the broker admits a validated Slack command for dispatch
- **THEN** it sends an `ExecutionRequest` containing all required fields to the worker

### Requirement: Execution Lifecycle Messages
The worker SHALL report the outcome of an `ExecutionRequest` using `ExecutionAccepted`,
`ExecutionProgress`, `ExecutionCompleted`, `ExecutionFailed`, or `ExecutionCancelled` messages,
each correlated to the originating `RequestId`.

#### Scenario: Worker reports accepted then completed
- **WHEN** the worker admits a request and the executor finishes successfully
- **THEN** the worker sends `ExecutionAccepted` followed by `ExecutionCompleted`, both carrying
  the original `RequestId`

#### Scenario: Worker reports a structured failure
- **WHEN** the executor invoked for a request fails
- **THEN** the worker sends `ExecutionFailed` carrying the original `RequestId` and a structured
  failure detail

### Requirement: Cancellation Message
The broker SHALL be able to send a `CancelExecution` message referencing a `RequestId`, and the
worker SHALL respond with a terminal outcome for that request.

#### Scenario: Cancel an in-flight execution
- **WHEN** the broker sends `CancelExecution` for a request that is still running
- **THEN** the worker responds with `ExecutionCancelled` for that `RequestId`

#### Scenario: Cancel an already-terminal execution
- **WHEN** the broker sends `CancelExecution` for a request that has already reached a terminal
  outcome
- **THEN** the worker does not emit a second, contradictory terminal message for that
  `RequestId`

### Requirement: Executor Status Query
The broker SHALL be able to send `ExecutorStatusRequest`, and the worker SHALL respond with
`ExecutorStatusResponse` describing configured executor state and capabilities.

#### Scenario: Status query returns current executor state
- **WHEN** the broker sends `ExecutorStatusRequest`
- **THEN** the worker responds with `ExecutorStatusResponse` reflecting each configured
  executor's current lifecycle state and capabilities

### Requirement: Connect and Disconnect Executor Messages
The broker SHALL be able to send `ConnectExecutor` and `DisconnectExecutor` messages for an
approved executor key, and the worker SHALL respond indicating success or failure.

#### Scenario: Connect an approved executor
- **WHEN** the broker sends `ConnectExecutor` for a configured, enabled executor key
- **THEN** the worker attempts the connection and responds with the resulting readiness state

#### Scenario: Connect request for an unknown executor key is rejected
- **WHEN** the broker sends `ConnectExecutor` for an executor key that is not configured
- **THEN** the worker responds with a failure and does not attempt any connection

### Requirement: Health Liveness
Both sides SHALL exchange `HealthPing`/`HealthPong` messages to detect a broken IPC session.

#### Scenario: Missed health response triggers reconnect
- **WHEN** a `HealthPing` receives no `HealthPong` within the configured liveness window
- **THEN** the sending side treats the IPC session as broken and initiates reconnection

### Requirement: Transport Framing
Messages SHALL be transmitted as individually framed UTF-8 JSON over a persistent Unix domain
socket connection, with each message deterministically delimited from the next.

#### Scenario: Consecutive messages are parsed independently
- **WHEN** multiple messages are sent back-to-back on the same connection
- **THEN** the receiving side parses each message independently without merging or truncating
  adjacent messages

### Requirement: Bounded Reconnection on IPC Unavailability
IPC unavailability SHALL be treated as a recoverable condition; the broker SHALL attempt to
reconnect to the worker using bounded backoff rather than terminating.

#### Scenario: Worker restarts and broker reconnects
- **WHEN** the worker process becomes unavailable and later becomes reachable again
- **THEN** the broker reestablishes the IPC connection without requiring a broker restart
