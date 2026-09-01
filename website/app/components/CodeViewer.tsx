'use client';

import dynamic from 'next/dynamic';

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

export default function CodeViewer({ code, language, label, height }: CodeViewerProps) {
  const editorHeight = height ?? Math.min(420, Math.max(96, code.split('\n').length * 20 + 36));

  return (
    <div className="monaco-viewer" style={{ height: editorHeight }}>
      <MonacoEditor
        language={language}
        height="100%"
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
        theme="vs-dark"
        value={code}
      />
    </div>
  );
}
