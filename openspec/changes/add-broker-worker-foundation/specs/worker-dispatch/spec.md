## Purpose

Defines how the worker turns an admitted execution request into executor lifecycle actions and
normalized events, independent of any specific executor implementation.

## ADDED Requirements

### Requirement: Executor Resolution
On receiving an execution request, the worker SHALL resolve the target executor via the registry
using the request's executor key before taking any other action.

#### Scenario: Request resolves to a registered executor
- **WHEN** the worker receives a request naming a registered, enabled executor key
- **THEN** it resolves that executor before performing any further processing

#### Scenario: Request for an unregistered executor key fails without invocation
- **WHEN** the worker receives a request naming an executor key that is not registered
- **THEN** it produces a failure outcome for the request and never invokes any executor

### Requirement: Admission Acknowledgement
The worker SHALL emit an accepted event once a request passes executor and operation validation,
or a failed event if it does not.

#### Scenario: Valid request is acknowledged before execution
- **WHEN** a request passes executor and operation validation
- **THEN** the worker emits an accepted event before invoking the executor

#### Scenario: Invalid request is failed without acceptance
- **WHEN** a request fails executor or operation validation
- **THEN** the worker emits a failed event and never emits an accepted event for that request

### Requirement: Event Forwarding
The worker SHALL forward each executor progress update and the executor's terminal result
(completed, failed, or cancelled) as a corresponding lifecycle event correlated to the
originating request.

#### Scenario: Progress updates forwarded in order
- **WHEN** an executor emits a sequence of progress updates while handling a request
- **THEN** the worker forwards each update as a lifecycle event in the same order

#### Scenario: Terminal result forwarded exactly once
- **WHEN** an executor returns its terminal result for a request
- **THEN** the worker forwards exactly one corresponding terminal lifecycle event for that
  request

### Requirement: Cancellation Propagation
On receiving a cancellation request for an in-flight execution, the worker SHALL propagate
cancellation to the executor and SHALL report a cancelled outcome once the executor confirms.

#### Scenario: Cancellation of an in-flight execution
- **WHEN** a cancellation request arrives for a request that is still executing
- **THEN** the worker propagates cancellation to the executor and, once the executor confirms,
  reports a cancelled outcome for that request

#### Scenario: Cancellation of an already-terminal execution is a no-op
- **WHEN** a cancellation request arrives for a request that has already reached a terminal
  outcome
- **THEN** the worker does not emit a second, contradictory terminal event for that request

### Requirement: Sequential Execution Per Target
The worker SHALL serialize executions that target the same repository or local mutable state, so
that no two such executions run concurrently.

#### Scenario: Same-target requests do not overlap
- **WHEN** two admitted requests target the same repository alias
- **THEN** the worker does not execute them concurrently; the second begins only after the first
  reaches a terminal outcome

### Requirement: Executor Status Reporting
The worker SHALL report each configured executor's current lifecycle state and capabilities on
request, independent of any single execution's outcome.

#### Scenario: Status query reflects current executor state
- **WHEN** the worker receives a status query for a configured executor
- **THEN** it returns that executor's current lifecycle state and capabilities, regardless of
  whether a request is currently executing
