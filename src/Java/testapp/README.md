# TestApp

Running the app will produce `SubmitOrder` that will be consumed by `SubmitOrderConsumer`.

The consumer uses `MyServiceImpl`, which randomly throws to simulate failures. When that happens, MyServiceBus publishes a `Fault<OrderSubmitted>` to the `submit-order_fault` queue, where `SubmitOrderFaultConsumer` logs the fault details. Consumers of `Fault<OrderSubmitted>` must subscribe to `submit-order_fault`; this fault queue differs from the `submit-order_error` queue, which stores failed messages that should not be re-published as-is.

## Running

From this directory, execute:

```bash
./run.sh
```

The script builds required modules and starts the app.

