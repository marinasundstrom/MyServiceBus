# TestApp

Running the app will produce `SubmitOrder` that will be consumed by `SubmitOrderConsumer`.

The consumer uses `MyServiceImpl`, which randomly throws to simulate failures. When that happens, the message is moved to the `submit-order_error` queue, and a handler forwards it back to `queue:submit-order` for another attempt.

## Running

From this directory, execute:

```bash
./run.sh
```

The script builds required modules and starts the app.

