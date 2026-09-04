'use client';

import { useState } from 'react';
import CodeViewer from './CodeViewer';

type LanguageTabsProps = {
  csharp: string;
  java: string;
  kotlin?: string;
  csharpLabel?: string;
  javaLabel?: string;
  kotlinLabel?: string;
  csharpLanguage?: string;
  javaLanguage?: string;
  kotlinLanguage?: string;
};

type Language = 'csharp' | 'java' | 'kotlin';

export default function LanguageTabs({
  csharp,
  java,
  kotlin,
  csharpLabel = 'C#',
  javaLabel = 'Java',
  kotlinLabel = 'Kotlin',
  csharpLanguage = 'csharp',
  javaLanguage = 'java',
  kotlinLanguage = 'kotlin',
}: LanguageTabsProps) {
  const [language, setLanguage] = useState<Language>('csharp');
  const options = [
    { id: 'csharp' as const, code: csharp, label: csharpLabel, editorLanguage: csharpLanguage },
    { id: 'java' as const, code: java, label: javaLabel, editorLanguage: javaLanguage },
    ...(kotlin
      ? [{ id: 'kotlin' as const, code: kotlin, label: kotlinLabel, editorLanguage: kotlinLanguage }]
      : []),
  ];
  const selected = options.find((option) => option.id === language) ?? options[0];

  return (
    <div className="docs-code-block">
      <div className="docs-code-toolbar">
        <div className="language-toggle" aria-label="Code language">
          {options.map((option) => (
            <button
              aria-pressed={language === option.id}
              className={language === option.id ? 'active' : ''}
              key={option.id}
              onClick={() => setLanguage(option.id)}
              type="button"
            >
              {option.label}
            </button>
          ))}
        </div>
        <span>{selected.label.toUpperCase()}</span>
      </div>
      <CodeViewer
        code={selected.code}
        label={`${selected.label} example`}
        language={selected.editorLanguage}
        showLanguageLabel={false}
      />
    </div>
  );
}
