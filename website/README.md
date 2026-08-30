# MyServiceBus documentation website

This project contains the public MyServiceBus documentation experience. It is
intentionally curated rather than generated from the complete repository
`docs/` tree.

## Local development

```bash
npm install
npm run dev
```

Create a production build with:

```bash
npm run build
```

## Content authoring

Documentation prose belongs in `content/` as MDX, a Markdown-compatible format
that also permits the small amount of structured HTML needed by the site. Route
files under `app/docs/` should only select content and apply presentation
components.

Use ordinary fenced code blocks in MDX. The global MDX component mapping renders
them with the read-only Monaco viewer, so content authors do not need to import or
configure an editor component. Internal Markdown links are routed through Next.js
and reusable presentation such as concept cards and next-page links is supplied by
`mdx-components.tsx`.

All documentation routes use this content/presentation split. The route files stay
as small wrappers so they can apply the shared documentation layout and, where
needed, route metadata. The interactive landing page remains in React.

## Independent publishing

The `Deploy documentation website` GitHub Actions workflow installs, builds,
and publishes this project without restoring, compiling, testing, or releasing
the MyServiceBus .NET and Java projects. Start it manually after the matching
preview packages have been published so installation instructions never point
to a version that is not yet available.

The workflow creates a static export and publishes the `out/` directory with
GitHub Pages. It uses the repository name as the production base path, so the
site works at `https://marinasundstrom.github.io/MyServiceBus/`. No hosting
credentials or repository secrets are required.

All interactivity runs in the browser. The production site has no server-side
runtime, API routes, or server-owned state.

The language switches and light/dark theme preference are client-side controls.
The selected theme is stored only in the visitor's browser.

Anonymous site usage is measured with Google Analytics 4 using measurement ID
`G-RQ2J1Y64JG`. The Google tag is loaded in the browser after the page becomes
interactive.

Repository development documents are not website source and must not be copied
into this project automatically.

The public site currently covers:

- introduction and getting started
- detailed messaging concepts covering contracts, intent, receive endpoints,
  topology, dispatch, requests, and reliability
- RabbitMQ transport behavior
- Amazon SQS/SNS preview behavior and its verified interoperability boundary
- .NET NativeAOT registration modes and current support boundary
- attributed C# consumer methods, parameter binding, and class-level discovery
- platform parity across the .NET runtime, C# generation, and Java
- application testing
- verified interoperability and supported-version boundaries

Internal architecture, specifications, proposals, design decisions, release
processes, and contributor guidance remain in the repository under `docs/`.
