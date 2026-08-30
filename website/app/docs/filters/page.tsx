import type { Metadata } from 'next';
import MarkdownPage from '../../components/MarkdownPage';
import Content from '../../../content/filters.mdx';

export const metadata: Metadata = {
  title: 'Filters and middleware · MyServiceBus',
  description: 'Add ordered validation, logging, retry, and other cross-cutting behavior to MyServiceBus handlers in C# and Java.',
};

export default function Filters() {
  return <MarkdownPage><Content /></MarkdownPage>;
}
