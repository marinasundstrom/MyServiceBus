# TestApp

Running the app will produce `SubmitOrder` that will be consumed by `SubmitOrderConsumer`.

The consumer uses `MyServiceImpl`, which randomly throws to simulate failures. When that happens, MyServiceBus publishes a `Fault<OrderSubmitted>` to the `submit-order_fault` queue, where `SubmitOrderFaultConsumer` logs the fault details.

## Running

From this directory, execute:

```bash
./run.sh
```

The script builds required modules and starts the app.

