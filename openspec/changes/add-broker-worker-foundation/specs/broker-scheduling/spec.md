## Purpose

Defines how the broker admits and buffers execution requests before forwarding them to the
worker, bounding memory use and providing explicit, user-visible backpressure.

## ADDED Requirements

### Requirement: Bounded Admission Queue
The broker SHALL buffer admitted requests in a bounded in-memory queue with a configured
capacity.

#### Scenario: Request admitted while capacity remains
- **WHEN** a validated request is submitted and the queue is below its configured capacity
- **THEN** the request is enqueued for dispatch

### Requirement: Explicit Backpressure
When the queue is at capacity, the broker SHALL reject the new request with a clear busy/retry
indication rather than blocking indefinitely or silently dropping it.

#### Scenario: Request submitted while queue is full
- **WHEN** a validated request is submitted while the queue is at its configured capacity
- **THEN** the broker rejects it with a busy/retry indication and does not enqueue it

### Requirement: FIFO Dispatch
Requests SHALL be dispatched to the worker in the order they were admitted.

#### Scenario: Two admitted requests dispatch in submission order
- **WHEN** two requests are admitted to the queue in sequence
- **THEN** they are forwarded to the worker in the same order they were admitted

### Requirement: Single Global Execution By Default
By default, the broker SHALL allow only one execution to be in flight at a time, holding
subsequently admitted requests in the queue until the active execution reaches a terminal
outcome.

#### Scenario: Second request waits for the first to finish
- **WHEN** a second request is admitted while an earlier request is still executing
- **THEN** the second request remains queued and is not dispatched until the first reaches a
  terminal outcome

### Requirement: State Cleanup on Terminal Outcome
The broker SHALL remove a request's active in-memory state once its terminal outcome
(completed, failed, or cancelled) has been delivered.

#### Scenario: Completed request state is cleared
- **WHEN** a request's final result has been delivered to its originating Slack thread
- **THEN** the broker no longer retains that request in its active in-memory state
