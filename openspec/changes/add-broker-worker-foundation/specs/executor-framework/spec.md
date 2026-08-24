## Purpose

Defines the common executor contract, its supporting types, the registry that resolves
executors by key, and a mock executor that lets the rest of the system be built and tested
before any real application-specific executor exists.

## ADDED Requirements

### Requirement: Executor Contract
Every executor SHALL implement connect, disconnect, and message operations, and SHALL declare a
stable executor key and its capabilities.

#### Scenario: Executor declares key and capabilities
- **WHEN** an executor is registered
- **THEN** it exposes a stable, non-empty executor key and a capabilities description that other
  components can query without invoking any operation

### Requirement: Idempotent Connect
Connecting SHALL be safe to invoke when the executor is already connected or ready.

#### Scenario: Connect called while already ready
- **WHEN** connect is invoked on an executor that is already in the Ready state
- **THEN** the executor remains Ready and no duplicate connection side effect occurs

### Requirement: Safe Disconnect After Partial Connect
Disconnecting SHALL be safe to invoke after a failed or partial connect, and SHALL NOT terminate
an externally managed application unless policy explicitly allows it.

#### Scenario: Disconnect after failed connect
- **WHEN** disconnect is invoked on an executor whose most recent connect attempt failed
- **THEN** disconnect completes without error and leaves no dangling connection resource

#### Scenario: Disconnect on an externally managed executor
- **WHEN** disconnect is invoked on an executor whose lifecycle policy marks it as externally
  managed
- **THEN** the executor's adapter resources are released but the underlying application is not
  stopped

### Requirement: Bounded Message Handling
Sending a message to an executor SHALL validate the requested operation against the executor's
declared capabilities, honor the supplied timeout and cancellation, and return a normalized
result rather than raising an unhandled exception for an expected failure.

#### Scenario: Unsupported operation rejected before execution
- **WHEN** a message names an operation the executor does not declare support for
- **THEN** the executor returns a normalized failure result without attempting the operation

#### Scenario: Operation exceeds its timeout
- **WHEN** a message's configured timeout elapses before the executor produces a result
- **THEN** the executor returns a normalized result indicating a timeout outcome

#### Scenario: Cancellation triggers cooperative stop
- **WHEN** the supplied cancellation signal is triggered while a message is in flight
- **THEN** the executor stops the operation and returns a normalized cancelled outcome

### Requirement: Progress Reporting
When an executor's declared capabilities include progress support, it SHALL emit normalized
progress updates (status, stage, timestamp) while a message is in flight, before returning its
terminal result.

#### Scenario: Long-running operation emits progress
- **WHEN** an executor that declares progress support handles a message that takes multiple
  steps
- **THEN** it emits at least one progress update before the terminal result

### Requirement: Executor Registry Resolution
The registry SHALL resolve an executor by its declared key and SHALL reject resolution for
unknown or disabled executor keys.

#### Scenario: Resolve a known, enabled executor
- **WHEN** a lookup is performed for a key that is configured and enabled
- **THEN** the registry returns that executor

#### Scenario: Resolve an unknown key
- **WHEN** a lookup is performed for a key that is not configured
- **THEN** the registry reports the key as unresolved and does not invoke any executor

#### Scenario: Resolve a disabled key
- **WHEN** a lookup is performed for a key that is configured but disabled
- **THEN** the registry reports the key as unresolved and does not invoke any executor

### Requirement: Executor Lifecycle States
Each executor SHALL report one of the following lifecycle states, distinct from any single
request's outcome: Disconnected, Connecting, Ready, Busy, Degraded, Faulted, Disconnecting.

#### Scenario: Status reflects an in-flight message
- **WHEN** an executor status query is made while that executor is actively handling a message
- **THEN** the reported state is Busy

### Requirement: Mock Executor Availability
A mock executor implementing the executor contract SHALL be available and configurable to
simulate success, failure, timeout, cancellation, and multi-step progress sequences, so that the
broker/worker pipeline and its automated tests do not depend on any real external application.

#### Scenario: Mock executor simulates failure
- **WHEN** the mock executor is configured to fail
- **THEN** invoking it returns a normalized failure result without contacting any external
  process or service

#### Scenario: Mock executor simulates a progress sequence
- **WHEN** the mock executor is configured with an ordered sequence of progress steps
- **THEN** invoking it emits each configured progress step in order before returning its
  terminal result

#### Scenario: Mock executor responds to cancellation
- **WHEN** the mock executor is configured to run indefinitely and a cancellation signal is
  triggered while it is in flight
- **THEN** it stops and returns a normalized cancelled outcome
