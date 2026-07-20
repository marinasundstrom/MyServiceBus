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

`/request` and `/request_multi` send a `TestRequest`. Their `*/fault` variants mark the request so `TestRequestConsumer` intentionally faults.

See `/Users/robert/Projects/MyServiceBus/src/TestApp/TestApp.http` for ready-made requests.
