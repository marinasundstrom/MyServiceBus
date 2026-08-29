import Link from 'next/link';
import CodeViewer from '../../components/CodeViewer';

const methodExample = `[Consumer("orders")]
public static class OrderConsumers
{
    public static Task ReceiveOrder(
        Order order,
        IOrderRepository orders,
        CancellationToken cancellationToken)
        => orders.Receive(order, cancellationToken);

    public static Task CancelOrder(
        CancelOrder command,
        IOrderRepository orders,
        CancellationToken cancellationToken)
        => orders.Cancel(command, cancellationToken);
}`;

const overrideExample = `[Consumer]
public static class OrderConsumers
{
    [Consumer("priority-orders")]
    public static Task ReceiveOrder(Order order, IOrderRepository orders)
        => orders.Receive(order);
}`;

const methodOnlyExample = `public static class OrderFunctions
{
    [Consumer("orders")]
    public static Task ReceiveOrder(Order order, IOrderRepository orders)
        => orders.Receive(order);
}`;

const conventionExample = `public static class OrderFunctions
{
    [Consumer]
    public static Task OrderSubmittedConsumer(ConsumeContext<OrderSubmitted> context)
        => Task.CompletedTask;
}`;

const fluentExample = `configurator.AddConsumerMethods(typeof(OrderConsumers), "receive-order");`;

const filteredScanExample = `configurator.AddConsumers(
    type => type.Namespace == "Sales.Consumers",
    typeof(OrderFunctions).Assembly);`;

const interfaceExample = `[Consumer("orders")]
public sealed class SubmitOrderConsumer : IConsumer<SubmitOrder>
{
    public Task Consume(ConsumeContext<SubmitOrder> context) => /* ... */;
}`;

const javaMethodExample = `public final class OrderConsumers {
    @MessageConsumer("orders")
    public static CompletionStage<Void> receiveOrder(
            Order order,
            ConsumeContext<Order> context,
            OrderRepository orders,
            CancellationToken cancellationToken) {
        return orders.receive(order, cancellationToken);
    }
}`;

const responseMethodExample = `public static class OrderConsumers
{
    [Consumer("submit-order")]
    public static Task<SubmitOrderResponse> SubmitOrder(
        SubmitOrder order,
        IOrderService orders,
        CancellationToken cancellationToken)
        => orders.Submit(order, cancellationToken);
}`;

const javaResponseMethodExample = `public final class OrderConsumers {
    @MessageConsumer("submit-order")
    public static CompletionStage<SubmitOrderResponse> submitOrder(
            SubmitOrder order,
            OrderService orders) {
        return orders.submit(order);
    }
}`;

const javaGeneratedExample = `dependencies {
    annotationProcessor "io.github.marinasundstrom.myservicebus:myservicebus-processor:0.1.0-preview.6"
}

GeneratedConsumerCatalog.INSTANCE.register(configurator);`;

export default function ConsumerMethods() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">C# and Java consumer declarations · Preview</p>
      <h1>Consume messages with methods, not framework base classes.</h1>
      <p className="docs-summary">
        A consumer can be a method: its attribute declares it, its signature binds the
        message and dependencies, and its body handles the message. No consumer interface,
        base class, or one-consumer-per-class structure is required.
      </p>

      <CodeViewer code={methodExample} label="C# consumer methods" language="csharp" />

      <h2>The corresponding Java declaration</h2>
      <p>
        Java uses <code>@MessageConsumer</code> because <code>Consumer&lt;T&gt;</code> already
        names its interface-consumer contract. Its binding and endpoint semantics match
        the C# declaration without requiring a shared attribute or interface.
      </p>
      <CodeViewer code={javaMethodExample} label="Java consumer method" language="java" />

      <h2>Return a response</h2>
      <p>
        A request handler can return its response contract directly. MyServiceBus awaits
        the operation and sends the value through the active consume context, preserving
        the normal request correlation metadata. C# supports <code>Task&lt;T&gt;</code> and
        <code>ValueTask&lt;T&gt;</code>; Java supports <code>CompletableFuture&lt;T&gt;</code> and
        <code>CompletionStage&lt;T&gt;</code>.
      </p>
      <CodeViewer code={responseMethodExample} label="C# request-response consumer method" language="csharp" />
      <CodeViewer code={javaResponseMethodExample} label="Java request-response consumer method" language="java" />
      <p>
        Returning a response does not make the context parameter mandatory. Add
        <code> ConsumeContext&lt;TMessage&gt;</code> when the method needs headers, correlation
        identifiers, addresses, or other receive metadata; otherwise the message and
        application services are enough.
      </p>
      <p>
        A response-bearing method requires an incoming request with a response address.
        If the method fails—or no response address is present—consumption fails through
        the normal retry and fault pipeline. Synchronous response values are not supported.
      </p>

      <h2>Choose by size and grouping</h2>
      <p>
        Use an <code>IConsumer&lt;T&gt;</code> class when a consumer is substantial enough to
        deserve its own type. Use consumer methods when the containing class mainly groups
        related handlers and acts as their de-facto namespace. Neither declaration style
        changes the message contract or wire behavior.
      </p>
      <p>
        C# currently requires methods to be contained by a type. That containing type is
        organizational and is not itself the consumer. Static methods do not cause the
        container to be instantiated or registered as a service.
      </p>

      <h2>Parameter binding</h2>
      <p>
        The method name does not determine eligibility or the message contract.
        <code>Consume</code>, <code>ReceiveOrder</code>, <code>Handle</code>, and other function
        names are all valid; the signature determines the binding.
      </p>
      <div className="docs-feature-grid">
        <div><span>01</span><h3>Message</h3><p>The first ordinary parameter identifies the consumed message contract.</p></div>
        <div><span>02</span><h3>Context</h3><p><code>ConsumeContext&lt;T&gt;</code> or <code>ConsumeContext</code> comes from the receive pipeline.</p></div>
        <div><span>03</span><h3>Services</h3><p>Additional ordinary parameters resolve from the dependency-injection scope created for the message.</p></div>
        <div><span>04</span><h3>Cancellation</h3><p><code>CancellationToken</code> binds to the active consume context.</p></div>
      </div>

      <h2>Group methods by endpoint</h2>
      <p>
        A class-level <code>[Consumer(&quot;orders&quot;)]</code> discovers all eligible methods on
        the class and maps them to one endpoint. Grouping is an optional organizational
        benefit: related handlers can stay together instead of being spread across separate
        consumer classes. The endpoint binds every declared message type; if two methods on
        that endpoint consume the same message type, both methods run.
      </p>

      <h2>Override or configure the mapping</h2>
      <p>
        The attribute string is the receive endpoint name. A method-level attribute can
        override the class mapping for an individual method, and an explicit fluent value
        overrides both. Explicit names are never replaced by an endpoint-name formatter.
      </p>
      <CodeViewer code={overrideExample} label="C# consumer method endpoint override" language="csharp" />
      <CodeViewer code={fluentExample} label="C# fluent consumer method mapping" language="csharp" />
      <p>
        A bare method-level <code>[Consumer]</code> still declares a consumer. Its endpoint
        name comes from the method name, so the example below maps to
        <code>OrderSubmittedConsumer</code>.
      </p>
      <CodeViewer code={conventionExample} label="C# method-name endpoint convention" language="csharp" />
      <p>
        Endpoint precedence is: explicit fluent mapping, method attribute, class attribute,
        then convention.
      </p>
      <p>
        Method containers do not implement an <code>IConsumer</code> marker or share
        a consumer interface with another framework.
      </p>

      <h2>Mark only the method</h2>
      <p>
        The containing class does not need an attribute. A method-level attribute is a
        complete declaration in both reflection and generated discovery, which also keeps
        the model suitable for external integrations that cannot annotate a container.
      </p>
      <CodeViewer code={methodOnlyExample} label="C# method-level consumer declaration" language="csharp" />

      <h2>Limit reflection scanning</h2>
      <p>
        Reflection discovery is already limited to the supplied assemblies. An optional
        type predicate can narrow both interface and method-consumer discovery further.
      </p>
      <CodeViewer code={filteredScanExample} label="C# filtered consumer discovery" language="csharp" />

      <h2>Override an interface consumer endpoint</h2>
      <p>
        The attribute is also an alternative to fluent endpoint mapping for a normal
        <code>IConsumer&lt;T&gt;</code> class. In that case it overrides the consumer&apos;s
        endpoint name; it does not turn <code>Consume</code> into a separately discovered
        method consumer.
      </p>
      <CodeViewer code={interfaceExample} label="C# interface consumer endpoint override" language="csharp" />

      <h2>Reflection or generated dispatch</h2>
      <p>
        MyServiceBus has two discovery and registration paths. Reflection inspects declarations
        at startup. The C# generator and Java JSR 269 processor scan their compilations and emit
        explicit typed registrations. Once registered, both paths enter the same runtime pipeline.
      </p>
      <ul className="check-list">
        <li>Assembly discovery builds method descriptors and invokes methods through reflection.</li>
        <li>The C# and Java generators bind parameters and call methods directly.</li>
        <li>Both modes use the same receive, retry, fault, telemetry, and dependency scopes.</li>
        <li>Java reflection inspects only explicitly registered classes; it does not scan the classpath.</li>
      </ul>
      <CodeViewer code={javaGeneratedExample} label="Java generated consumer registration" language="java" />

      <div className="callout">
        <strong>Interoperability boundary</strong>
        <p>
          Consumer declaration syntax stays local to each language. Wire compatibility
          is determined by message identity, envelopes, headers, topology, and transport
          behavior. A future compatibility adapter can translate another framework&apos;s
          conventions without coupling the core runtime to its interfaces.
        </p>
      </div>

      <div className="callout">
        <strong>External Raven consideration</strong>
        <p>
          Raven is outside MyServiceBus. If it consumes this descriptor model through a
          separate integration, its namespace-level functions should be considered before
          classes rather than importing a C# declaration convention.
        </p>
      </div>

      <div className="next-card">
        <div><span>Next</span><strong>Compare support across runtimes and tooling</strong></div>
        <Link href="/docs/platform-parity">Platform parity →</Link>
      </div>
    </article>
  );
}
