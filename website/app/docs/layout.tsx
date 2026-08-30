import Link from 'next/link';
import DocsHeader from '../components/DocsHeader';

const sections = [
  {
    label: 'Start here',
    links: [
      ['Introduction', '/docs'],
      ['Why MyServiceBus?', '/docs/why-myservicebus'],
      ['Getting started', '/docs/getting-started'],
      ['Mediator pattern', '/docs/mediator'],
      ['Migrate from MassTransit', '/docs/migrate-from-masstransit'],
      ['Migrate from MediatR', '/docs/migrate-from-mediatr'],
      ['Java adoption', '/docs/java-adoption'],
    ],
  },
  {
    label: 'Concepts',
    links: [
      ['Overview', '/docs/concepts'],
      ['Messages and contracts', '/docs/concepts/messages'],
      ['Send, publish, request', '/docs/concepts/message-intent'],
      ['Receive endpoints', '/docs/concepts/receive-endpoints'],
      ['Routing and topology', '/docs/concepts/routing-topology'],
      ['Consumers and dispatch', '/docs/concepts/consumers'],
      ['Requests and responses', '/docs/concepts/requests'],
      ['Reliability and faults', '/docs/concepts/reliability'],
    ],
  },
  {
    label: 'Guides',
    links: [
      ['Distributed systems fundamentals', '/docs/distributed-systems'],
      ['RabbitMQ transport', '/docs/rabbitmq'],
      ['Azure Service Bus', '/docs/azure-service-bus'],
      ['Amazon SQS/SNS', '/docs/amazon-sqs'],
      ['Transactional outbox', '/docs/transactional-outbox'],
      ['Message scheduling', '/docs/scheduling'],
      ['Consumer methods', '/docs/consumer-methods'],
      ['.NET 11 and unions', '/docs/dotnet-11-unions'],
      ['Platform parity', '/docs/platform-parity'],
      ['Supported versions', '/docs/supported-versions'],
      ['AOT compilation', '/docs/native-aot'],
      ['Runtime monitoring', '/docs/runtime-monitoring'],
      ['Testing', '/docs/testing'],
      ['Interoperability', '/docs/interoperability'],
      ['NServiceBus', '/docs/nservicebus'],
    ],
  },
];

export default function DocumentationLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="docs-site">
      <DocsHeader />
      <div className="docs-frame">
        <aside className="docs-sidebar" aria-label="Documentation navigation">
          {sections.map((section) => (
            <section key={section.label}>
              <h2>{section.label}</h2>
              <nav>
                {section.links.map(([label, href]) => (
                  <Link key={href} href={href}>{label}</Link>
                ))}
              </nav>
            </section>
          ))}
          <div className="sidebar-note">
            <span className="pulse" aria-hidden="true" />
            <div><strong>0.1.0-preview.6</strong><br />Latest preview</div>
          </div>
        </aside>
        <main className="docs-main">{children}</main>
      </div>
    </div>
  );
}
