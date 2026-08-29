# Delivery Failure Matrix

## Purpose

This matrix turns the [Delivery Guarantees Specification](../specs/delivery-guarantees.md) into executable C# and Java scenarios. A checked item requires an automated assertion of the broker state, application outcome, message identity, and settlement result; observing only an exception or log entry is insufficient.

Status values:

- **Verified**: an existing automated test proves the complete scenario.
- **Partial**: related coverage exists but does not inject the named failure or assert every required outcome.
- **Open**: no sufficient automated evidence was found.

## Producer Acceptance

| ID | Scenario | Required outcome | RabbitMQ C# | RabbitMQ Java | Azure C# | Azure Java |
| --- | --- | --- | --- | --- | --- | --- |
| P01 | Broker accepts directed send | API succeeds only after broker and routing acceptance | Partial | Partial | Partial | Partial |
| P02 | Directed destination is missing or unroutable | API fails; no success hook is emitted | Partial | Partial | Partial | Partial |
| P03 | Broker accepts publish with no subscribers | API succeeds after broker acceptance | Partial | Partial | Partial | Partial |
| P04 | Broker negatively acknowledges or rejects producer | API fails with a transport-specific cause wrapped by the public transport exception | Partial | Partial | Partial | Partial |
| P05 | Connection fails before broker acceptance | API fails; retry with the same message identity is possible | Open | Open | Open | Open |
| P06 | Connection fails after acceptance but before client acknowledgement | API reports an ambiguous failure; retry can create at most a detectable duplicate with the same identity | Open | Open | Open | Open |
| P07 | Caller cancellation before send begins | No broker message; cancellation remains distinguishable from transport failure | Partial | Partial | Partial | Partial |
| P08 | Caller cancellation races broker acceptance | Outcome is documented as accepted or ambiguous; never reported as proven rejection | Open | Open | Open | Open |

RabbitMQ P01 through P06 require publisher confirms. Directed sends additionally require mandatory-return handling or an equivalent topology-specific proof of routing. Event publication must continue to allow zero bound queues.

## Successful Consumption and Redelivery

| ID | Scenario | Required outcome | RabbitMQ C# | RabbitMQ Java | Azure C# | Azure Java |
| --- | --- | --- | --- | --- | --- | --- |
| C01 | Handler succeeds | Source settles once after completion | Partial | Partial | Verified | Verified |
| C02 | Process exits before handler starts | Source becomes available to another receiver | Open | Open | Open | Open |
| C03 | Process exits during handler | Source is redelivered; message identity is unchanged | Open | Open | Open | Open |
| C04 | Process exits after handler effect but before settlement | Source is redelivered and duplicate application execution is detectable | Open | Open | Open | Open |
| C05 | Settlement response is lost after broker commit | Runtime tolerates a duplicate or reports settlement ambiguity without losing identity | Open | Open | Open | Open |
| C06 | Malformed envelope cannot be resolved | Source is not acknowledged as success; configured preservation policy is applied | Open | Open | Open | Open |
| C07 | Several consumers share one endpoint and one fails | Source follows terminal-failure policy after every selected consumer is attempted | Partial | Partial | Open | Open |

Existing happy-path and competing-consumer tests provide partial evidence, but they do not replace process-level crash injection at the named boundaries.

## Retry and Terminal Failure

| ID | Scenario | Required outcome | RabbitMQ C# | RabbitMQ Java | Azure C# | Azure Java |
| --- | --- | --- | --- | --- | --- | --- |
| R01 | Handler fails once and then succeeds | Same source delivery is attempted twice and settled after success | Verified | Verified | Verified | Verified |
| R02 | Retry policy exhausts | Configured total attempt count occurs; terminal policy runs once | Verified | Verified | Verified | Verified |
| R03 | Process exits during retry delay | Source becomes available for broker redelivery; in-process delay is not claimed durable | Open | Open | Open | Open |
| R04 | Delivery lock approaches expiry during handler/retry | Lock is renewed or source is safely redelivered without silent loss | Not applicable | Not applicable | Verified | Verified |
| R05 | Cancellation occurs during retry delay | No new in-process attempt starts; source remains eligible for broker redelivery | Partial | Partial | Open | Open |

## Error and Skipped Preservation

| ID | Scenario | Required outcome | RabbitMQ C# | RabbitMQ Java | Azure C# | Azure Java |
| --- | --- | --- | --- | --- | --- | --- |
| E01 | Terminal handler failure and `_error` accepts copy | Copy preserves body, identity, routing, and failure metadata; source then settles | Partial | Partial | Verified | Verified |
| E02 | `_error` rejects copy before acceptance | Source is not settled and is redelivered | Partial | Partial | Open | Open |
| E03 | `_error` accepts copy but client sees ambiguous failure | Source can redeliver; duplicate `_error` copies retain one identity | Open | Open | Open | Open |
| E04 | Source settlement fails after confirmed `_error` copy | Source redelivers; duplicate error copy is detectable and no original is lost | Open | Open | Open | Open |
| E05 | Fault publication fails | Original still follows `_error` preservation policy; fault notification is not treated as preservation | Open | Open | Open | Open |
| S01 | Unknown type and `_skipped` accepts copy | Unchanged copy is accepted before source settlement | Partial | Partial | Verified | Verified |
| S02 | `_skipped` rejects copy | Source is not settled and is redelivered | Partial | Partial | Open | Open |
| S03 | `_skipped` acceptance is ambiguous | Duplicate skipped copies are detectable by stable identity; original is not silently lost | Open | Open | Open | Open |

The RabbitMQ implementation now releases the source when preservation is unconfirmed and requires routing for error, fault, and skipped compatibility exchanges. It remains ineligible for production promotion until E02 and S02 are proven against a real broker.

## Consume-and-Produce Consistency

| ID | Scenario | Required outcome before outbox/inbox promotion | C# | Java |
| --- | --- | --- | --- | --- |
| O01 | Application transaction commits; outgoing send fails | Outbox retains undispatched intent and later dispatches with the original identity | Open | Open |
| O02 | Outgoing message is accepted; dispatcher exits before marking it sent | Redispatch produces a detectable duplicate with the same identity | Open | Open |
| O03 | Consumer effect commits; process exits before source settlement | Inbox prevents the effect from being applied twice and permits safe source settlement | Open | Open |
| O04 | Two replicas process the same message identity concurrently | Inbox storage admits one effect owner and gives the loser a defined outcome | Open | Open |
| O05 | Outbox or inbox schema upgrade occurs during rolling deployment | Supported adjacent versions continue safely or startup fails before processing | Open | Open |
| O06 | Cleanup races dispatch or duplicate detection | Undispatched work and active deduplication records are never removed early | Open | Open |

These scenarios require a supported persistence provider and a real transactional database. In-memory substitutes are not release evidence.

## Request and Response

| ID | Scenario | Required outcome | RabbitMQ C# | RabbitMQ Java | Azure C# | Azure Java |
| --- | --- | --- | --- | --- | --- | --- |
| Q01 | Response endpoint is ready before request send | Fast response is correlated and not missed | Verified | Verified | Verified | Verified |
| Q02 | Consumer returns a response | Exactly one result is presented to the waiting API; duplicates are ignored or harmless | Verified | Verified | Verified | Verified |
| Q03 | Consumer returns a fault | Waiting API receives a correlated `RequestFaultException` | Verified | Verified | Verified | Verified |
| Q04 | Client times out after broker accepted request | Client stops waiting; server may finish; late response is safely discarded | Partial | Partial | Partial | Partial |
| Q05 | Client exits while request is processing | Request remains a normal broker delivery; response endpoint loss does not lose the original request | Open | Open | Open | Open |
| Q06 | Duplicate request delivery | Idempotent handler/inbox prevents duplicate business effect; responses remain correlatable | Open | Open | Open | Open |
| Q07 | Response send outcome is ambiguous | Request follows terminal policy or redelivery; duplicate responses retain correlation and identity | Open | Open | Open | Open |

## Shutdown and Overload

| ID | Scenario | Required outcome | RabbitMQ C# | RabbitMQ Java | Azure C# | Azure Java |
| --- | --- | --- | --- | --- | --- | --- |
| L01 | Graceful stop with no active work | Receiver stops accepting deliveries and closes cleanly | Partial | Partial | Partial | Partial |
| L02 | Graceful stop with active work under deadline | New deliveries stop; active work completes and settles before stop returns | Partial | Partial | Open | Partial |
| L03 | Drain deadline expires | Stop reports forced termination; unfinished sources remain eligible for redelivery | Open | Open | Open | Open |
| L04 | Handler never completes | Stop remains bounded by the configured deadline | Open | Open | Open | Open |
| L05 | Load exceeds configured concurrency | In-flight and queued work remain within declared bounds; broker backpressure is observable | Open | Open | Open | Open |
| L06 | Broker disconnects during drain | Completed and unfinished deliveries reach documented settlement or redelivery outcomes | Open | Open | Open | Open |

## Scheduling

| ID | Scenario | Required outcome | C# | Java |
| --- | --- | --- | --- | --- |
| T01 | In-process scheduled delivery fires | Message is sent once within the documented timing tolerance while the process remains alive | Verified | Verified |
| T02 | Schedule is cancelled before firing | No message is sent | Verified | Verified |
| T03 | Process exits before due time | Current emulated schedule is documented as lost; no durable claim is made | Open | Open |
| T04 | Durable scheduler is introduced and process restarts | Pending intent survives and dispatches with stable identity | Not implemented | Not implemented |
| T05 | Dispatch result is ambiguous | Retry produces a detectable duplicate rather than a new identity | Open | Open |

## Test Harness Requirements

The failure harness should provide deterministic control points rather than timing-only tests:

- before producer write
- after broker acceptance but before client completion
- before consumer callback
- after application handler completion but before source settlement
- before and after `_error` or `_skipped` acceptance
- before and after source settlement
- during retry delay and lock renewal
- when receiver stop begins and when its drain deadline expires

Use disposable real brokers for acceptance and settlement evidence. Protocol mocks remain useful for exception translation and call ordering, but they cannot prove broker durability, routing, redelivery, or recovery.

Every scenario must record:

- original and observed message identifiers
- handler attempt count and replica identity
- producer result or exception category
- source entity state
- error, skipped, fault, and response entity state where applicable
- whether the outcome is confirmed, rejected, or ambiguous
- relevant trace and correlation identifiers

## Promotion Rule

The first production profile does not require every row for every optional capability. It does require every applicable producer, consumption, retry, preservation, shutdown, and overload row to be **Verified** in both C# and Java. Request, scheduling, outbox, inbox, and other capabilities may be excluded only when the transport descriptor and public documentation clearly label them unsupported or non-production.
