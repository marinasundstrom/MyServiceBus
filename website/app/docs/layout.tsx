import Link from 'next/link';
import DocsHeader from '../components/DocsHeader';

const sections = [
  {
    label: 'Start here',
    links: [
      ['Introduction', '/docs'],
      ['Why MyServiceBus?', '/docs/why-myservicebus'],
      ['Getting started', '/docs/getting-started'],
    ],
  },
  {
    label: 'Learn',
    links: [
      ['Core concepts', '/docs/concepts'],
      ['Distributed systems fundamentals', '/docs/distributed-systems'],
      ['RabbitMQ transport', '/docs/rabbitmq'],
      ['Azure Service Bus', '/docs/azure-service-bus'],
      ['Consumer methods', '/docs/consumer-methods'],
      ['Platform parity', '/docs/platform-parity'],
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
            <div><strong>0.1.0-preview.4</strong><br />Latest preview</div>
          </div>
        </aside>
        <main className="docs-main">{children}</main>
      </div>
    </div>
  );
}
