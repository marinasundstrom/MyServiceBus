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

## Independent publishing

The `Deploy documentation website` GitHub Actions workflow installs, builds,
and publishes this project without restoring, compiling, testing, or releasing
the MyServiceBus .NET and Java projects. It runs when files under `website/`
change on `main`, and it can also be started manually.

The workflow creates a static export and publishes the `out/` directory with
GitHub Pages. It uses the repository name as the production base path, so the
site works at `https://marinasundstrom.github.io/MyServiceBus/`. No hosting
credentials or repository secrets are required.

All interactivity runs in the browser. The production site has no server-side
runtime, API routes, or server-owned state.

The language switches and light/dark theme preference are client-side controls.
The selected theme is stored only in the visitor's browser.

Repository development documents are not website source and must not be copied
into this project automatically.

The public site currently covers:

- introduction and getting started
- core messaging concepts
- RabbitMQ transport behavior
- application testing
- verified interoperability and supported-version boundaries

Internal architecture, specifications, proposals, design decisions, release
processes, and contributor guidance remain in the repository under `docs/`.
