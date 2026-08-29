import type { Metadata } from 'next';
import MarkdownPage from '../../components/MarkdownPage';
import Content from '../../../content/scheduling.mdx';

export const metadata: Metadata = {
  title: 'Message scheduling · MyServiceBus',
  description: 'Choose explicit volatile in-memory scheduling, durable PostgreSQL outbox intent, or a custom message-aware provider in C# and Java.',
};

export default function Scheduling() {
  return <MarkdownPage><Content /></MarkdownPage>;
}
