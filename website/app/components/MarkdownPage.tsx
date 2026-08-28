export default function MarkdownPage({ children }: { children: React.ReactNode }) {
  return <article className="docs-article markdown-content">{children}</article>;
}
