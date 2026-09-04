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
  title: 'MyServiceBus — Messaging for .NET and the JVM',
  description:
    'A pre-1.0 messaging runtime for .NET and the JVM, with stable C# and Java projections, experimental Kotlin support, broker transports, and scoped MassTransit interoperability.',
  openGraph: {
    title: 'MyServiceBus — Messaging for .NET and the JVM',
    description:
      'Evaluate stable C# and Java projections and experimental Kotlin support with explicit transport, interoperability, maturity, and support boundaries.',
    type: 'website',
    url: siteUrl,
    images: [{ url: `${siteUrl}/og.png`, width: 1730, height: 909, alt: 'MyServiceBus — Messaging for .NET and the JVM' }],
  },
  twitter: {
    card: 'summary_large_image',
    title: 'MyServiceBus — Messaging for .NET and the JVM',
    description:
      'Evaluate stable C# and Java projections and experimental Kotlin support with explicit transport, interoperability, maturity, and support boundaries.',
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
