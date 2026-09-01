# TestApp

The sample app exposes explicit success and fault endpoints for publish, send, and request scenarios.

Common routes:

- `/publish`
- `/publish/fault`
- `/send`
- `/send/fault`
- `/request`
- `/request/fault`
- `/request_multi`
- `/request_multi/fault`
- `/dashboard/v1/overview`
- `/dashboard/v1/messages`
- `/dashboard/v1/consumers`
- `/dashboard/v1/topology`

The `/dashboard/v1/*` routes expose stable JSON DTOs for dashboard experiments. They include bus address metadata, registered messages, queue bindings, and consumer settings without leaking runtime-specific objects such as `Type` instances or delegates.

`/publish` and `/send` produce `SubmitOrder` messages. The `*/fault` variants mark the message so `SubmitOrderConsumer` intentionally throws and a `Fault<SubmitOrder>` is published to the `submit-order_fault` queue, where `SubmitOrderFaultConsumer` logs it.

The sample declares workflows at several levels of complexity for the monitoring Dashboard:

- `sample-local-order-observation` is a single application-owned terminal reaction.
- `sample-order-submission` is a shared C# and Java fan-out choreography.
- `sample-fulfillment-handoff` is a linear C# → Java → C# handoff. Start a run with `POST /workflows/fulfillment`.

`/request` and `/request_multi` send a `TestRequest`. Their `*/fault` variants mark the request so `TestRequestConsumer` intentionally faults.

See `/Users/robert/Projects/MyServiceBus/src/TestApp/TestApp.http` for ready-made requests.

## Order orchestration sample

`POST /workflows/orchestration` starts `OrderOrchestrationStateMachine`, an experimental volatile saga hosted by the C# service:

1. the coordinator consumes `OrderOrchestrationStarted` and sends `OrchestrationInventoryRequested` to the Java service;
2. Java publishes `OrchestrationInventoryReserved` after its local inventory reaction;
3. the coordinator sends `OrchestrationPaymentRequested` to a C# participant;
4. the participant publishes `OrchestrationPaymentCaptured`; and
5. the coordinator publishes `OrderOrchestrationCompleted` and finalizes the instance.

The sample deliberately keeps inventory and payment behavior in their participant services. The saga owns only the cross-service process, its correlation identity, and current state. Its repository is process-local and volatile, so this demonstrates the authoring and bus-execution path rather than production durability.
