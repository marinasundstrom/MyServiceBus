# Mediator and In-Memory Conformance Matrix

## Purpose

This matrix tracks the matching C# and Java scenarios required by the [Mediator and In-Memory Stability Gate](in-memory-stability-gate.md). It is the working compatibility checklist for the local mediator runtime and the in-memory test harness.

Status meanings:

- **Verified** — matching observable behavior is covered in both reference clients.
- **Partial** — both clients cover part of the contract, but a shared scenario is still missing.
- **Gap** — the portable behavior must be decided or implemented before the gate can close.

Platform-specific syntax and asynchronous wrappers are not parity gaps when the underlying behavior is equivalent and documented.

## Shared scenario matrix

| # | Portable scenario | C# coverage | Java coverage | Status | Remaining work |
|---|---|---|---|---|---|
| 1 | Start, stop, repeated lifecycle calls, and operations outside the valid lifecycle | `InMemoryHarnessDiTests.Lifecycle_is_idempotent_and_operations_require_started_state`; `MediatorTransportFactoryTests.Hosted_bus_lifecycle_is_idempotent_and_operations_require_started_state`; failed-start state assertion in `TransportCapabilityTests` | `InMemoryHarnessDiTest.lifecycleIsIdempotentAndOperationsRequireStartedState`; hosted lifecycle coverage in `MessageBusLoggingTest`; failed-start state assertion in `TransportCapabilityTest`; standalone mediator dispatch remains immediately usable | **Verified** | Preserve the distinction between immediately usable standalone mediators and explicitly started hosted buses. |
| 2 | Directed send and publish fan-out | `MediatorTransportFactoryTests.Send_Invokes_RegisteredHandler`; `MultipleConsumersTests` | `MediatorTransportFactoryTest.publishDeliversMessageToConsumer`; `MultipleConsumersTest` in runtime and testing modules | **Partial** | Add one shared directed-send scenario and one explicit publish fan-out scenario against each local runtime. |
| 3 | Consumer scope creation and disposal per delivery | `InMemoryHarnessDiTests.Creates_and_disposes_a_consumer_scope_per_delivery`; `FilterDiTests` | `InMemoryHarnessDiTest.createsAndDisposesAConsumerScopePerDelivery`; `MediatorTransportFactoryTest.scopedSendEndpointProviderRetainsConsumeContextAcrossAsyncDispatch`; `ScopeConsumerFactory` tests | **Verified** | Preserve per-delivery scope identity and keep each scope alive through asynchronous consumer completion. |
| 4 | Request/response correlation, timeout, cancellation, and fault response | `GenericRequestClientTests`, including timeout/cancellation distinction; `InMemoryHarnessDiTests.Request_and_correlation_identifiers_flow_through_response`; `RequestFaultExceptionTests` | `GenericRequestClientPolicyTest`; `InMemoryHarnessDiTest.requestAndCorrelationIdentifiersFlowThroughResponse` and `concurrentRequestsMatchOnlyResponsesWithTheirRequestIdentifier`; `RequestClientFaultTest`; `RequestClientHeaderTest` | **Verified** | Preserve request-specific response matching and distinguish elapsed deadlines, caller cancellation, and fault responses. |
| 5 | Retry attempts, delay, terminal failure, and no-retry behavior | `PipeTests`; mediator retry-order, exhaustion, and no-retry scenarios | `PipeConfiguratorTest`; matching mediator retry-order, exhaustion, and no-retry scenarios | **Verified** | Exception selection, attempt metadata, and redelivery remain separate future features. |
| 6 | Send, publish, and consume filter order | `PipeTests`; `OutboundFilterOrderingTests`; `MediatorTransportFactoryTests` | `PipeConfiguratorTest`; `ServiceBusPublishFilterTest`; `MediatorTransportFactoryTest` | **Verified** | Extend only when another runtime pipeline stage becomes public. |
| 7 | Headers, correlation, cancellation, and telemetry context | `PublishHeaderTests`; `GenericRequestClientTests`; `OpenTelemetryFilterTests`; `PipeTests` | `PublishHeaderTest`; `RequestClientHeaderTest`; `OpenTelemetryFilterTest`; `PipeConfiguratorTest` | **Partial** | Add one integrated mediator/harness scenario that carries headers, correlation, and cancellation into the consumer together. |
| 8 | Interface and inherited message-type dispatch | `MediatorTransportFactoryTests.Publish_dispatches_to_concrete_interface_and_base_consumers_once`; `InMemoryHarnessDiTests.Dispatches_concrete_messages_to_interface_and_base_handlers_once`; anonymous interface tests | `MediatorTransportFactoryTest.publishDispatchesToConcreteInterfaceAndBaseConsumersOnce`; `InMemoryHarnessDiTest.dispatchesConcreteMessagesToInterfaceAndBaseHandlersOnce` | **Verified** | Preserve concrete, implemented-interface, and non-root-base dispatch with at-most-once consumer invocation. |
| 9 | Scheduled delivery and cancellation | `SchedulingTests.SchedulePublish_delays_message` and `Cancel_prevents_scheduled_publish` | `SchedulingTest.scheduleSend_delays_message` and `cancelScheduledSend_preventsDelivery` | **Partial** | Align send-versus-publish scenarios and introduce deterministic timing control before claiming ordering guarantees. |
| 10 | Concurrent dispatch, ordering, and handler failure | `InMemoryHarnessDiTests.Should_record_concurrent_delivery_deterministically`; `MultipleConsumersFaultTests` | `InMemoryHarnessDiTest.records_concurrent_delivery_deterministically`; multiple-consumer tests | **Partial** | Define ordering and multi-handler failure guarantees, then verify the same outcome in both harnesses. |
| 11 | Stable topology snapshots and truthful capabilities | `TopologySnapshotTests`; `TransportCapabilityTests.InMemory_factory_exposes_its_descriptor` | `TopologySnapshotTest`; `TransportCapabilityTest.mediatorExposesItsDescriptor` | **Verified** | Keep descriptors synchronized when local-runtime behavior changes. |
| 12 | Equivalent harness observations and assertion timing | Concurrent consumed-record assertions and request-client tests in `InMemoryHarnessDiTests` | Concurrent consumed-record assertions and request-client tests in `InMemoryHarnessDiTest` | **Partial** | Define observation categories and eventual-assertion timeout behavior as a shared testing contract. |

## Next compatibility slices

Work should close the remaining rows in dependency order:

1. integrate header, correlation, cancellation, and telemetry propagation
2. define concurrency, ordering, failure, and harness observation guarantees
3. align scheduled send/publish scenarios using deterministic time controls

A row moves to **Verified** only when both implementations have matching behavioral tests and the public documentation states any intentional platform distinction.
