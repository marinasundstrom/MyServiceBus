'use client';

import dynamic from 'next/dynamic';
import type { Monaco } from '@monaco-editor/react';

const MonacoEditor = dynamic(() => import('@monaco-editor/react'), {
  ssr: false,
  loading: () => <div className="code-loading">Loading code…</div>,
});

type CodeViewerProps = {
  code: string;
  language: string;
  label: string;
  height?: number;
};

function defineCodeTheme(monaco: Monaco) {
  monaco.editor.defineTheme('myservicebus-dark', {
    base: 'vs-dark',
    inherit: true,
    rules: [
      { token: 'comment', foreground: '6A9955', fontStyle: 'italic' },
      { token: 'string', foreground: 'CE9178' },
      { token: 'number', foreground: 'B5CEA8' },
      { token: 'constant', foreground: '569CD6' },
      { token: 'keyword', foreground: '569CD6' },
      { token: 'type', foreground: '4EC9B0' },
      { token: 'function', foreground: 'DCDCAA' },
      { token: 'parameter', foreground: '9CDCFE' },
      { token: 'operator', foreground: 'D4D4D4' },
      { token: 'invalid', foreground: 'F44747' },
    ],
    colors: {},
  });
}

export default function CodeViewer({ code, language, label, height }: CodeViewerProps) {
  const editorHeight = height ?? Math.min(420, Math.max(96, code.split('\n').length * 20 + 36));
  const isRaven = language === 'raven';

  return (
    <div className="monaco-viewer" style={{ height: editorHeight }}>
      <MonacoEditor
        beforeMount={(monaco) => {
          defineCodeTheme(monaco);
          if (isRaven && !monaco.languages.getLanguages().some(({ id }) => id === 'raven')) {
            monaco.languages.register({ id: 'raven', aliases: ['Raven'], extensions: ['.rvn', '.rav'] });
          }
        }}
        language={language}
        height="100%"
        onMount={(editor, monaco) => {
          if (!isRaven) {
            return;
          }

          void import('./ravenSyntax')
            .then(({ configureRavenSyntax }) => configureRavenSyntax(monaco))
            .then(() => {
              const model = editor.getModel();
              if (model && !model.isDisposed()) {
                monaco.editor.setModelLanguage(model, 'raven');
              }
            })
            .catch((error: unknown) => {
              console.error('Unable to load Raven syntax highlighting.', error);
            });
        }}
        options={{
          accessibilitySupport: 'auto',
          ariaLabel: label,
          automaticLayout: true,
          contextmenu: true,
          domReadOnly: true,
          folding: false,
          fontFamily: 'var(--font-geist-mono), ui-monospace, monospace',
          fontSize: 12,
          glyphMargin: false,
          lineDecorationsWidth: 16,
          lineHeight: 20,
          lineNumbers: 'on',
          lineNumbersMinChars: 2,
          minimap: { enabled: false },
          padding: { top: 16, bottom: 16 },
          readOnly: true,
          renderLineHighlight: 'none',
          scrollBeyondLastLine: false,
          scrollbar: { alwaysConsumeMouseWheel: false, horizontalScrollbarSize: 8, verticalScrollbarSize: 8 },
          selectionHighlight: false,
          stickyScroll: { enabled: false },
          wordWrap: 'off',
        }}
        theme="myservicebus-dark"
        value={code}
      />
    </div>
  );
}
