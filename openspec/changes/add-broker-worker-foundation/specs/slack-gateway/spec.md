## Purpose

Defines how the broker receives operator commands from Slack over Socket Mode, decides whether
to admit them, and routes progress and results back to the originating Slack thread.

## ADDED Requirements

### Requirement: Socket Mode Connectivity
The broker SHALL receive Slack commands and interactive payloads over a Socket Mode WebSocket
connection, without exposing a public inbound HTTP endpoint.

#### Scenario: Command delivered without an inbound HTTP listener
- **WHEN** a user invokes an approved Slack command
- **THEN** the broker receives it over the Socket Mode connection with no public inbound HTTP
  endpoint involved

### Requirement: Caller Authorization
The broker SHALL reject a request from a Slack user who is not on the configured allowlist of
authorized users/roles, before any execution request is created.

#### Scenario: Unauthorized user's command is rejected
- **WHEN** a Slack command is received from a user not on the authorized allowlist
- **THEN** the broker rejects the command and does not create an execution request

### Requirement: Executor and Operation Validation
The broker SHALL reject a request that names an unconfigured executor key, or an operation not
in that executor's allowed set, before dispatch.

#### Scenario: Unknown executor key is rejected
- **WHEN** a Slack command names an executor key that is not configured
- **THEN** the broker rejects the command in Slack and does not dispatch it

#### Scenario: Disallowed operation is rejected
- **WHEN** a Slack command names a known executor but an operation not in that executor's
  allowed operation set
- **THEN** the broker rejects the command in Slack and does not dispatch it

### Requirement: Alias-Only Targeting
The broker SHALL resolve any repository or service target through a configured alias and SHALL
NOT accept a raw path or endpoint supplied directly from Slack.

#### Scenario: Raw path is rejected
- **WHEN** a Slack command supplies a raw filesystem path or endpoint instead of a configured
  alias
- **THEN** the broker rejects the command and does not resolve it to any target

### Requirement: Thread-Scoped Delivery
Progress updates and final results for a request SHALL be posted to the Slack channel and thread
that originated the request.

#### Scenario: Progress posted to the originating thread
- **WHEN** the worker reports progress for a request
- **THEN** the broker posts that update to the same Slack channel and thread the request came
  from, and not to any other thread

### Requirement: User-Visible Failure Reporting
Queue-full, IPC-unavailable, executor-unavailable, timeout, and cancellation outcomes SHALL be
visible to the requesting user in Slack, with the failure class distinguishable from the others.

#### Scenario: Queue-full rejection is visible in Slack
- **WHEN** a command is rejected because the broker's admission queue is full
- **THEN** the requesting user sees a Slack message indicating the system is busy

#### Scenario: IPC unavailability is visible in Slack
- **WHEN** a command cannot be dispatched because the worker is unreachable
- **THEN** the requesting user sees a Slack message distinct from a generic error, indicating the
  local worker is unavailable

### Requirement: Confirmation for High-Impact Actions
An operation marked as high-impact (for example, an apply/mutate action) SHALL require an
explicit Slack confirmation step before the broker dispatches it.

#### Scenario: High-impact operation withheld pending confirmation
- **WHEN** a Slack command invokes a high-impact operation without prior confirmation
- **THEN** the broker does not dispatch it and instead prompts for confirmation

#### Scenario: High-impact operation dispatched after confirmation
- **WHEN** the requesting user explicitly confirms a previously prompted high-impact operation
- **THEN** the broker dispatches it

### Requirement: Sensitive Data Redaction
Content posted to Slack SHALL have secrets, tokens, and other configured sensitive patterns
redacted before delivery.

#### Scenario: Token pattern is redacted before delivery
- **WHEN** an executor result contains text matching a configured sensitive pattern
- **THEN** the broker redacts that text before posting the result to Slack
