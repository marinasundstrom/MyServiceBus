import Link from 'next/link';
import ThemeSelector from './ThemeSelector';

export default function DocsHeader() {
  return (
    <header className="topbar docs-topbar">
      <Link className="brand" href="/" aria-label="MyServiceBus home">
        <span className="brand-mark" aria-hidden="true">M</span>
        <span>MyServiceBus</span>
        <span className="docs-label">Docs</span>
      </Link>
      <nav className="topnav" aria-label="Documentation links">
        <Link href="/docs/getting-started">Get started</Link>
        <a href="https://github.com/marinasundstrom/MyServiceBus">GitHub ↗</a>
        <ThemeSelector />
      </nav>
    </header>
  );
}
