# TestApp

Running the app will produce `SubmitOrder` messages that are consumed by `SubmitOrderConsumer`.

The sample no longer uses randomized failures. Failure cases are isolated to explicit routes such as `/publish/fault`, `/send/fault`, `/request/fault`, and `/request_multi/fault`. When a submit-order fault occurs, MyServiceBus publishes a `Fault<SubmitOrder>` to the `submit-order_fault` queue, where `SubmitOrderFaultConsumer` logs the fault details.

## Running

From this directory, execute:

```bash
./run.sh
```

The script builds required modules and starts the app.
