import type { Metadata } from 'next';
import { Geist, Geist_Mono } from 'next/font/google';
import GoogleAnalytics from './components/GoogleAnalytics';
import './globals.css';

const geistSans = Geist({
  variable: '--font-geist-sans',
  subsets: ['latin'],
});

const geistMono = Geist_Mono({
  variable: '--font-geist-mono',
  subsets: ['latin'],
});

const isGitHubPages = process.env.GITHUB_ACTIONS === 'true';
const repositoryName = process.env.GITHUB_REPOSITORY?.split('/')[1] ?? 'MyServiceBus';
const repositoryOwner = process.env.GITHUB_REPOSITORY_OWNER ?? 'marinasundstrom';
const siteUrl = isGitHubPages
  ? `https://${repositoryOwner}.github.io/${repositoryName}`
  : 'http://localhost:3000';

export const metadata: Metadata = {
  title: 'MyServiceBus — Messaging for .NET and Java',
  description:
    'A pre-1.0 messaging runtime for .NET and Java with scoped MassTransit interoperability, broker transports, and in-process mediator dispatch.',
  openGraph: {
    title: 'MyServiceBus — Messaging for .NET and Java',
    description:
      'Evaluate a pre-1.0 C# and Java messaging runtime with explicit transport, interoperability, maturity, and support boundaries.',
    type: 'website',
    url: siteUrl,
    images: [{ url: `${siteUrl}/og.png`, width: 1730, height: 909, alt: 'MyServiceBus — Messaging for .NET and Java' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'MyServiceBus — Messaging for .NET and Java',
    description:
      'Evaluate a pre-1.0 C# and Java messaging runtime with explicit transport, interoperability, maturity, and support boundaries.',
    images: [`${siteUrl}/og.png`],
  },
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html data-scroll-behavior="smooth" lang="en" suppressHydrationWarning>
      <body
        className={`${geistSans.variable} ${geistMono.variable} antialiased`}
      >
        {children}
        <GoogleAnalytics />
      </body>
    </html>
  );
}
