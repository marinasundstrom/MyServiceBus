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
      "implementation 'io.github.marinasundstrom.myservicebus:myservicebus-rabbitmq:0.1.0-preview.6'",
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
            <p className="eyebrow">Asynchronous messaging for .NET and Java</p>
            <h1>One messaging model for distributed services.</h1>
            <p className="lede">
              Publish events and send commands across C# and Java with RabbitMQ
              or Azure Service Bus. Integrate Java into a MassTransit estate or
              build cross-platform from the start—with MediatR-compatible
              in-process dispatch when you need it.
            </p>
            <div className="hero-actions">
              <Link className="primary-button" href="/docs/getting-started">
                Get started <span aria-hidden="true">→</span>
              </Link>
              <Link className="text-button" href="/docs/why-myservicebus">
                Why MyServiceBus?
              </Link>
            </div>
            <div className="compatibility-line">
              <span aria-hidden="true" className="pulse" />
              RabbitMQ verified · Azure Service Bus verified preview
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
                height={72}
                label={`${example.label} install command`}
                language={language === 'csharp' ? 'shell' : 'groovy'}
              />
              <CodeViewer
                code={example.code}
                height={278}
                label={`${example.label} quick start`}
                language={language}
              />
              <Link className="continue-link" href="/docs/getting-started">
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
              <p>Choose C# or Java per service while keeping one permissively licensed messaging core.</p>
            </article>
          </div>
        </section>

        <section className="concept-strip" id="concepts" aria-labelledby="concept-heading">
          <div className="section-intro">
            <p className="eyebrow">The essentials</p>
            <h2 id="concept-heading">A small set of concepts that travel well.</h2>
          </div>
          <div className="concept-grid">
            <article>
              <span className="concept-number">01</span>
              <h3>Publish events</h3>
              <p>Fan out facts to every interested service without coupling producers to queues.</p>
            </article>
            <article>
              <span className="concept-number">02</span>
              <h3>Send commands</h3>
              <p>Direct work to a specific endpoint when exactly one consumer should handle it.</p>
            </article>
            <article>
              <span className="concept-number">03</span>
              <h3>Consume safely</h3>
              <p>Use scoped consumers, retries, faults, and test harnesses with aligned semantics.</p>
            </article>
          </div>
        </section>

        <section className="explore-section" aria-labelledby="explore-heading">
          <div className="explore-heading">
            <p className="eyebrow">Focused documentation</p>
            <h2 id="explore-heading">From first message to production behavior.</h2>
            <p>Learn the portable concepts first, then add transport and testing detail when you need it.</p>
          </div>
          <div className="explore-grid">
            <Link href="/docs/getting-started"><span>4 steps</span><h3>Getting started</h3><p>Install, configure, consume, and publish in C# or Java.</p><b>Open guide →</b></Link>
            <Link href="/docs/concepts"><span>Core model</span><h3>Messaging concepts</h3><p>Choose between publish, send, consume, and request.</p><b>Learn concepts →</b></Link>
            <Link href="/docs/rabbitmq"><span>Transport</span><h3>RabbitMQ</h3><p>Understand recovery, failure queues, topology, and tuning.</p><b>Configure transport →</b></Link>
            <Link href="/docs/azure-service-bus"><span>Preview transport</span><h3>Azure Service Bus</h3><p>Provision Azure, configure either client, and understand the verified interoperability boundary.</p><b>Configure the transport →</b></Link>
            <Link href="/docs/testing"><span>Confidence</span><h3>Testing</h3><p>Exercise message flows with the aligned in-memory harness.</p><b>Write a test →</b></Link>
            <Link href="/docs/transactional-outbox"><span>Production reliability</span><h3>Transactional outbox</h3><p>Commit PostgreSQL application state and outgoing messaging intent together in C# or Java.</p><b>Explore the outbox →</b></Link>
            <Link href="/docs/mediator"><span>In process</span><h3>Mediator pattern</h3><p>Use generated dispatch for local commands, queries, and notifications through reusable consumers and pipelines.</p><b>Use the mediator →</b></Link>
            <Link href="/docs/native-aot"><span>Work in progress</span><h3>AOT compilation</h3><p>Generate consumer dispatch for .NET NativeAOT and GraalVM Native Image.</p><b>See the proof of concept →</b></Link>
          </div>
        </section>
      </main>

      <footer className="site-footer">
        <div className="brand"><span className="brand-mark" aria-hidden="true">M</span><span>MyServiceBus</span></div>
        <p>Lightweight messaging for services that cross ecosystem boundaries.</p>
        <div><Link href="/docs">Documentation</Link><Link href="/docs/interoperability">Compatibility</Link><a href="https://github.com/marinasundstrom/MyServiceBus">GitHub ↗</a></div>
      </footer>
    </div>
  );
}
