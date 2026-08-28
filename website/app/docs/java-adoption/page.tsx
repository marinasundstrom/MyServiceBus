import Link from 'next/link';
import CodeViewer from '../../components/CodeViewer';

const decoratorExample = `ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
    .addServiceBus(cfg -> {
        cfg.addConsumer(SubmitOrderConsumer.class);
        cfg.using(RabbitMqFactoryConfigurator.class,
            (context, rabbit) -> {
                rabbit.host("localhost");
                rabbit.configureEndpoints(context);
            });
    });

ServiceProvider provider = services.buildServiceProvider();
MessageBus bus = provider.getRequiredService(MessageBus.class);
bus.start();`;

const springExample = `@Configuration
class MessagingConfiguration {
    @Bean(initMethod = "start", destroyMethod = "stop")
    MessageBus messageBus(SubmitOrderConsumer consumer) {
        return MessageBus.factory.create(
            RabbitMqFactoryConfigurator.class,
            rabbit -> {
                rabbit.host("localhost");
                rabbit.receiveEndpoint("submit-order", endpoint ->
                    endpoint.handler(SubmitOrder.class,
                        consumer::consume));
            });
    }
}

@Component
final class SubmitOrderConsumer {
    private final OrderRepository orders;

    SubmitOrderConsumer(OrderRepository orders) {
        this.orders = orders;
    }

    CompletableFuture<Void> consume(
            ConsumeContext<SubmitOrder> context) {
        return orders.submit(context.getMessage());
    }
}`;

const cdiExample = `@ApplicationScoped
final class SubmitOrderConsumer {
    @Inject
    OrderRepository orders;

    CompletableFuture<Void> consume(
            ConsumeContext<SubmitOrder> context) {
        return orders.submit(context.getMessage());
    }
}

@ApplicationScoped
class MessagingConfiguration {
    @Produces
    @ApplicationScoped
    MessageBus messageBus(SubmitOrderConsumer consumer)
            throws Exception {
        MessageBus bus = MessageBus.factory.create(
            RabbitMqFactoryConfigurator.class,
            rabbit -> {
                rabbit.host("localhost");
                rabbit.receiveEndpoint("submit-order", endpoint ->
                    endpoint.handler(SubmitOrder.class,
                        consumer::consume));
            });
        bus.start();
        return bus;
    }

    void stop(@Disposes MessageBus bus) throws Exception {
        bus.stop();
    }
}`;

const daggerExample = `@Singleton
@Component
interface ApplicationComponent {
    SubmitOrderConsumer submitOrderConsumer();
}

final class SubmitOrderConsumer {
    private final OrderRepository orders;

    @Inject
    SubmitOrderConsumer(OrderRepository orders) {
        this.orders = orders;
    }

    CompletableFuture<Void> consume(
            ConsumeContext<SubmitOrder> context) {
        return orders.submit(context.getMessage());
    }
}

ApplicationComponent application =
    DaggerApplicationComponent.create();
SubmitOrderConsumer consumer =
    application.submitOrderConsumer();

MessageBus bus = MessageBus.factory.create(
    RabbitMqFactoryConfigurator.class,
    rabbit -> {
        rabbit.host("localhost");
        rabbit.receiveEndpoint("submit-order", endpoint ->
            endpoint.handler(SubmitOrder.class,
                consumer::consume));
    });

bus.start();`;

const applicationFactoryExample = `Supplier<SubmitOrderConsumer> consumers =
    () -> applicationServices.createSubmitOrderConsumer();

MessageBus bus = MessageBus.factory.create(
    RabbitMqFactoryConfigurator.class,
    rabbit -> {
        rabbit.host("localhost");
        rabbit.receiveEndpoint("submit-order", endpoint ->
            endpoint.handler(SubmitOrder.class, context ->
                consumers.get().consume(context)));
    });

bus.start();`;

export default function JavaAdoption() {
  return (
    <article className="docs-article">
      <p className="docs-kicker">Java adoption</p>
      <h1>Bring your container. Keep it in charge.</h1>
      <p className="docs-summary">
        The MyServiceBus service collection and decorator structure are a container-neutral
        programming model. Materialize that collection with the included provider or an
        adapter backed by your framework. For a lighter integration into an existing
        application, use the bus factory and let its current container construct consumers.
      </p>

      <div className="concept-comparison">
        <section>
          <span className="tag">MYSERVICEBUS-FIRST</span>
          <h2>Decorator style</h2>
          <p>
            Use <code>ServiceCollection.from(...)</code> to structure a service graph
            managed by MyServiceBus. This is the recommended new-project style and is
            deliberately familiar to developers moving between the C# and Java clients.
          </p>
        </section>
        <section>
          <span className="tag">EXISTING JAVA APP</span>
          <h2>Bus factory</h2>
          <p>
            Use the bus factory as the default integration path for an application that
            already owns a container. Close over its managed consumers or factories without
            making the application resolve through the MyServiceBus provider abstraction.
          </p>
        </section>
      </div>

      <h2 id="new-project">New project: decorator style</h2>
      <p>
        Start with the MyServiceBus service abstractions when the application does not
        already have a competing composition model. Decorators provide the Java equivalent
        of the extension-method structure used by the C# client.
      </p>
      <CodeViewer code={decoratorExample} label="MyServiceBus decorator setup" language="java" />

      <div className="callout callout-accent">
        <strong>Guice is not an application requirement</strong>
        <p>
          <code>ServiceCollection.create()</code> currently uses Guice as its included
          implementation. The bus-factory integrations below do not connect Spring, CDI,
          Dagger, or an application container to that Guice instance. General registration
          factories use the JDK-standard <code>Supplier</code>; framework providers adapt
          through method references without exposing Guice types.
        </p>
      </div>

      <h2 id="spring">Spring</h2>
      <p>
        Define the bus as a Spring bean and inject an ordinary Spring-managed consumer into
        the configuration method. Spring constructs the consumer and its repository; the
        bean lifecycle starts and stops the bus.
      </p>
      <CodeViewer code={springExample} label="Spring integration" language="java" />

      <h2 id="jakarta-cdi">Jakarta CDI</h2>
      <p>
        A CDI producer can build and start the bus from injected application beans, while a
        disposer method stops it. This uses standard CDI APIs and applies to CDI-based
        runtimes such as Weld and Quarkus.
      </p>
      <CodeViewer code={cdiExample} label="Jakarta CDI integration" language="java" />

      <h2 id="dagger">Dagger</h2>
      <p>
        Dagger&apos;s generated component remains the object-graph owner. Expose the consumer
        as a component entry point and capture that instance in the bus configuration.
      </p>
      <CodeViewer code={daggerExample} label="Dagger integration" language="java" />

      <h2 id="application-factory">Application-owned factory</h2>
      <p>
        A framework is not required. Any supplier or application service that constructs a
        consumer can be the integration boundary. Use a per-message factory when the
        application needs a fresh consumer for every delivery.
      </p>
      <CodeViewer code={applicationFactoryExample} label="Application-owned consumer factory" language="java" />

      <div className="callout">
        <strong>Let the application container own its lifetimes</strong>
        <p>
          A captured singleton consumer must be safe for concurrent delivery. If the
          application container uses request, prototype, dependent, or custom scopes, put
          creation and cleanup behind an application-owned factory appropriate for that
          framework. MyServiceBus should not guess those lifecycle rules.
        </p>
      </div>

      <h2 id="provider-adapter">Materialize the collection with another container</h2>
      <p>
        A framework adapter can implement the neutral MyServiceBus collection and provider
        contracts, translate the registrations produced by the same decorators, and return a
        provider backed by Spring, CDI, Dagger, or another container. This preserves the
        MyServiceBus programming model while replacing the included Guice-backed
        materialization. An adapter must preserve the target container&apos;s lifetime, scope,
        cleanup, and multi-binding semantics.
      </p>
      <ul className="check-list">
        <li><code>SINGLETON</code> maps to one instance across the root provider and message scopes.</li>
        <li><code>SCOPED</code> maps to one instance per MyServiceBus message scope.</li>
        <li><code>TRANSIENT</code> maps to a new instance per resolution under the framework&apos;s ownership rules.</li>
        <li>The adapter closes its native scope only after asynchronous consumption completes.</li>
        <li>Multi-bindings and provider-aware factories retain the active scoped provider.</li>
      </ul>

      <h2 id="provider-conventions">Provider conventions without namespace lock-in</h2>
      <p>
        Java&apos;s common <code>Provider&lt;T&gt;</code> contract represents one binding,
        not a complete container or scope model. MyServiceBus therefore accepts the
        JDK-standard <code>Supplier&lt;? extends T&gt;</code> for zero-argument service
        factories. A <code>javax.inject.Provider</code>, <code>jakarta.inject.Provider</code>,
        Spring <code>ObjectProvider</code>, or Dagger provider can be passed as a method
        reference such as <code>provider::get</code> or <code>provider::getObject</code>.
        This preserves the familiar factory convention without choosing one side of the
        <code>javax</code>/<code>jakarta</code> package split.
      </p>

      <p className="small-note">
        Framework API references:{' '}
        <a href="https://docs.spring.io/spring-framework/reference/core/beans/basics.html">Spring container</a>,{' '}
        <a href="https://jakarta.ee/specifications/cdi/4.1/">Jakarta CDI</a>, and{' '}
        <a href="https://dagger.dev/dev-guide/basic-usage">Dagger components</a>.
      </p>

      <div className="next-card">
        <div><span>Next</span><strong>Choose reflection or generated registration</strong></div>
        <Link href="/docs/native-aot">AOT compilation →</Link>
      </div>
    </article>
  );
}
