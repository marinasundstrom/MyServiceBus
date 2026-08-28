import Link from 'next/link';
import { isValidElement, type ComponentPropsWithoutRef, type ReactElement, type ReactNode } from 'react';
import type { MDXComponents } from 'mdx/types';
import CodeViewer from './app/components/CodeViewer';

type CodeElementProps = {
  children?: string;
  className?: string;
};

function MarkdownLink({ href, ...props }: ComponentPropsWithoutRef<'a'>) {
  if (href?.startsWith('/')) {
    return <Link href={href} {...props} />;
  }

  return <a href={href} {...props} />;
}

function MarkdownCodeBlock({ children }: ComponentPropsWithoutRef<'pre'>) {
  if (!isValidElement(children)) {
    return <pre>{children}</pre>;
  }

  const code = children as ReactElement<CodeElementProps>;
  const value = String(code.props.children ?? '').replace(/\n$/, '');
  const language = code.props.className?.replace(/^language-/, '') ?? 'plaintext';

  return <CodeViewer code={value} label={`${language} code example`} language={language} />;
}

function ConceptCard({
  children,
  href,
  label,
  title,
}: {
  children: ReactNode;
  href: string;
  label: string;
  title: string;
}) {
  return (
    <Link href={href}>
      <span>{label}</span>
      <h2>{title}</h2>
      <p>{children}</p>
      <b>Read concept →</b>
    </Link>
  );
}

function NextCard({ href, label = 'Next', title }: { href: string; label?: string; title: string }) {
  return (
    <div className="next-card">
      <div><span>{label}</span><strong>{title}</strong></div>
      <Link href={href}>{title} →</Link>
    </div>
  );
}

const components: MDXComponents = {
  a: MarkdownLink,
  ConceptCard,
  NextCard,
  pre: MarkdownCodeBlock,
};

export function useMDXComponents(): MDXComponents {
  return components;
}
