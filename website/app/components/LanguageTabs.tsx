'use client';

import { useState } from 'react';
import CodeViewer from './CodeViewer';

type LanguageTabsProps = {
  csharp: string;
  java: string;
  csharpLabel?: string;
  javaLabel?: string;
  csharpLanguage?: string;
  javaLanguage?: string;
};

export default function LanguageTabs({
  csharp,
  java,
  csharpLabel = 'C#',
  javaLabel = 'Java',
  csharpLanguage = 'csharp',
  javaLanguage = 'java',
}: LanguageTabsProps) {
  const [language, setLanguage] = useState<'csharp' | 'java'>('csharp');
  const code = language === 'csharp' ? csharp : java;
  const editorLanguage = language === 'csharp' ? csharpLanguage : javaLanguage;

  return (
    <div className="docs-code-block">
      <div className="docs-code-toolbar">
        <div className="language-toggle" aria-label="Code language">
          <button
            className={language === 'csharp' ? 'active' : ''}
            onClick={() => setLanguage('csharp')}
            type="button"
          >
            {csharpLabel}
          </button>
          <button
            className={language === 'java' ? 'active' : ''}
            onClick={() => setLanguage('java')}
            type="button"
          >
            {javaLabel}
          </button>
        </div>
        <span>{language === 'csharp' ? 'C#' : 'JAVA'}</span>
      </div>
      <CodeViewer
        code={code}
        label={`${language === 'csharp' ? csharpLabel : javaLabel} example`}
        language={editorLanguage}
      />
    </div>
  );
}
