'use client';

import { useState } from 'react';
import Link from 'next/link';
import CodeViewer from './components/CodeViewer';
import HeroMessagingDiagram from './components/HeroMessagingDiagram';
import ThemeSelector from './components/ThemeSelector';

const examples = {
  csharp: {
    label: 'C#',
    install: 'dotnet add package Sundstrom.MyServiceBus.RabbitMq',
    installLanguage: 'shell',
    guide: '/docs/getting-started',
    code: `builder.Services.AddServiceBus(x =>
{
    x.AddConsumer<SubmitOrderConsumer>();
    x.UsingRabbitMq((context, cfg) =>
        cfg.ConfigureEndpoints(context));
});

await bus.Publish(new SubmitOrder(Guid.NewGuid()));`,
  },
  java: {
    label: 'Java',
    install:
      "implementation 'io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.9'",
    installLanguage: 'groovy',
    guide: '/docs/getting-started',
    code: `ServiceCollection services = ServiceCollection.create();

services.from(MessageBusServices.class)
    .addServiceBus(cfg -> {
        cfg.addConsumer(SubmitOrderConsumer.class);
        cfg.using(RabbitMqFactoryConfigurator.class,
            (context, rabbit) ->
                rabbit.configureEndpoints(context));
    });

MessageBus bus = services.buildServiceProvider()
    .getRequiredService(MessageBus.class);
bus.publish(new SubmitOrder(UUID.randomUUID()));`,
  },
  kotlin: {
    label: 'Kotlin',
    install: `implementation("io.github.marinasundstrom.myservicebus:myservicebus-kotlin:0.1.0-preview.9")
implementation("io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.9")`,
    installLanguage: 'kotlin',
    guide: '/docs/kotlin',
    code: `val services = ServiceCollection.create()

services.addServiceBus {
    consumer<SubmitOrderConsumer>()
    transport<RabbitMqFactoryConfigurator> { context ->
        configureEndpoints(context)
    }
}

val bus = services.buildServiceProvider()
    .getRequiredService<MessageBus>()
bus.start()

runBlocking {
    bus.publish(SubmitOrder(UUID.randomUUID()))
}`,
  },
};

type Language = keyof typeof examples;

export default function Home() {
  const [language, setLanguage] = useState<Language>('csharp');
  const example = examples[language];

  return (
    <div className="site-shell">
      <header className="topbar">
        <a className="brand" href="#top" aria-label="MyServiceBus home">
          <span className="brand-mark" aria-hidden="true">
            M
          </span>
          <span>MyServiceBus</span>
          <span className="preview-badge">preview</span>
        </a>
        <nav className="topnav" aria-label="Primary navigation">
          <Link href="/docs">Docs</Link>
          <Link href="/docs/concepts">Concepts</Link>
          <a href="https://github.com/marinasundstrom/MyServiceBus">GitHub ↗</a>
          <ThemeSelector />
        </nav>
      </header>

      <main id="top">
        <section className="hero">
          <div className="hero-copy">
            <p className="eyebrow">Asynchronous messaging for .NET and the JVM</p>
            <h1>One messaging model for distributed services.</h1>
            <p className="lede">
              Model commands, events, requests, and consumers consistently across
              C#, Java, and Kotlin. RabbitMQ is the verified broker baseline; Azure Service
              Bus and Amazon SQS/SNS are preview transports. A local mediator covers
              deliberately in-process dispatch without requiring a broker.
            </p>
            <div className="hero-actions">
              <Link className="primary-button" href="/docs/getting-started">
                Get started <span aria-hidden="true">→</span>
              </Link>
              <Link className="text-button" href="/docs/why-myservicebus">
                Evaluate the fit
              </Link>
            </div>
            <div className="compatibility-line">
              <span aria-hidden="true" className="pulse" />
              Pre-1.0 · RabbitMQ verified · Azure and AWS preview transports
            </div>
          </div>

          <div className="hero-showcase">
            <HeroMessagingDiagram />

            <div className="code-card" id="getting-started">
              <div className="code-card-header">
                <div className="language-toggle" aria-label="Code language">
                  {(Object.keys(examples) as Language[]).map((key) => (
                    <button
                      className={language === key ? 'active' : ''}
                      key={key}
                      onClick={() => setLanguage(key)}
                      type="button"
                    >
                      {examples[key].label}
                    </button>
                  ))}
                </div>
                <span className="step-label">01 — QUICK START</span>
              </div>
              <CodeViewer
                code={example.install}
                height={language === 'kotlin' ? 92 : 72}
                label={`${example.label} install command`}
                language={example.installLanguage}
                showLanguageLabel={false}
              />
              <CodeViewer
                code={example.code}
                height={278}
                label={`${example.label} quick start`}
                language={language}
                showLanguageLabel={false}
              />
              <Link className="continue-link" href={example.guide}>
                Continue the {example.label} guide <span aria-hidden="true">→</span>
              </Link>
            </div>
          </div>
        </section>

        <section className="concept-strip" aria-labelledby="adoption-heading">
          <div className="section-intro">
            <p className="eyebrow">Where it fits</p>
            <h2 id="adoption-heading">Three ways to adopt MyServiceBus.</h2>
          </div>
          <div className="concept-grid">
            <article>
              <span className="concept-number">01</span>
              <h3>Extend a .NET estate</h3>
              <p>Connect Java services to MassTransit through the documented common interoperability subset.</p>
            </article>
            <article>
              <span className="concept-number">02</span>
              <h3>Replace MediatR</h3>
              <p>Handle local commands, queries, and notifications through dedicated APIs and generated dispatch.</p>
            </article>
            <article>
              <span className="concept-number">03</span>
              <h3>Start cross-platform</h3>
              <p>Choose C#, Java, or Kotlin per service while keeping one permissively licensed messaging core.</p>
            </article>
          </div>
        </section>

        <section className="concept-strip" id="concepts" aria-labelledby="concept-heading">
          <div className="section-intro">
            <p className="eyebrow">Before you adopt</p>
            <h2 id="concept-heading">Know the current boundaries.</h2>
          </div>
          <div className="concept-grid">
            <article>
              <span className="concept-number">01</span>
              <h3>Pre-1.0 APIs</h3>
              <p>Preview releases can change configuration and public APIs before a stable compatibility policy is published.</p>
            </article>
            <article>
              <span className="concept-number">02</span>
              <h3>Focused scope</h3>
              <p>The portable subset is smaller than MassTransit and does not replace its feature breadth, maturity, or commercial support.</p>
            </article>
            <article>
              <span className="concept-number">03</span>
              <h3>Transport evidence</h3>
              <p>Ordering, settlement, topology, quotas, and operational behavior still need validation against the broker you will run.</p>
            </article>
          </div>
        </section>

        <section className="explore-section" aria-labelledby="explore-heading">
          <div className="explore-heading">
            <p className="eyebrow">Technical documentation</p>
            <h2 id="explore-heading">Evaluate the model, then implement it.</h2>
            <p>Start with fit and compatibility boundaries, then move into concepts, transport behavior, reliability, and testing.</p>
          </div>
          <div className="explore-grid">
            <Link href="/docs/why-myservicebus"><span>Decision guide</span><h3>Evaluate the fit</h3><p>Compare use cases, preview maturity, support expectations, alternatives, and adoption risk.</p><b>Review the trade-offs →</b></Link>
            <Link href="/docs/getting-started"><span>4 steps</span><h3>Getting started</h3><p>Install, configure, consume, and publish in C#, Java, or Kotlin.</p><b>Open guide →</b></Link>
            <Link href="/docs/concepts"><span>Core model</span><h3>Messaging concepts</h3><p>Choose between publish, send, consume, and request.</p><b>Learn concepts →</b></Link>
            <Link href="/docs/rabbitmq"><span>Transport</span><h3>RabbitMQ</h3><p>Understand recovery, failure queues, topology, and tuning.</p><b>Configure transport →</b></Link>
            <Link href="/docs/azure-service-bus"><span>Preview transport</span><h3>Azure Service Bus</h3><p>Provision Azure, configure either client, and understand the verified interoperability boundary.</p><b>Configure the transport →</b></Link>
            <Link href="/docs/testing"><span>Confidence</span><h3>Testing</h3><p>Exercise message flows with the aligned in-memory harness.</p><b>Write a test →</b></Link>
            <Link href="/docs/transactional-outbox"><span>Production reliability</span><h3>Transactional outbox</h3><p>Commit PostgreSQL application state and outgoing messaging intent together in C# or Java.</p><b>Explore the outbox →</b></Link>
            <Link href="/docs/mediator"><span>In process</span><h3>Mediator pattern</h3><p>Use generated dispatch for local commands, queries, and notifications through reusable consumers and pipelines.</p><b>Use the mediator →</b></Link>
          </div>
        </section>
      </main>

      <footer className="site-footer">
        <div className="brand"><span className="brand-mark" aria-hidden="true">M</span><span>MyServiceBus</span></div>
        <p>Cross-platform messaging with explicit compatibility and maturity boundaries.</p>
        <div><Link href="/docs">Documentation</Link><Link href="/docs/interoperability">Compatibility</Link><a href="https://github.com/marinasundstrom/MyServiceBus">GitHub ↗</a></div>
      </footer>
    </div>
  );
}
