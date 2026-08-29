import type { Metadata } from 'next';
import MarkdownPage from '../../components/MarkdownPage';
import Content from '../../../content/transactional-outbox.mdx';

export const metadata: Metadata = {
  title: 'Transactional outbox and inbox · MyServiceBus',
  description: 'Coordinate PostgreSQL application state and messaging intent with aligned C# and Java outbox and inbox persistence.',
};

export default function TransactionalOutbox() {
  return <MarkdownPage><Content /></MarkdownPage>;
}
