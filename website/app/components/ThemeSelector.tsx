'use client';

import { useEffect, useState } from 'react';

type ThemeSetting = 'system' | 'light' | 'dark';

function resolveTheme(setting: ThemeSetting) {
  if (setting !== 'system') {
    return setting;
  }

  return window.matchMedia('(prefers-color-scheme: dark)').matches
    ? 'dark'
    : 'light';
}

function applyTheme(setting: ThemeSetting) {
  document.documentElement.dataset.themeSetting = setting;
  document.documentElement.dataset.theme = resolveTheme(setting);
}

export default function ThemeSelector() {
  const [setting, setSetting] = useState<ThemeSetting>('system');

  useEffect(() => {
    const savedSetting = window.localStorage.getItem('myservicebus-theme');
    const initialSetting: ThemeSetting = savedSetting === 'light' || savedSetting === 'dark'
      ? savedSetting
      : 'system';

    applyTheme(initialSetting);
    const updateSetting = window.requestAnimationFrame(() => setSetting(initialSetting));
    return () => window.cancelAnimationFrame(updateSetting);
  }, []);

  useEffect(() => {
    if (setting !== 'system') {
      return;
    }

    const media = window.matchMedia('(prefers-color-scheme: dark)');
    const updateSystemTheme = () => applyTheme('system');
    media.addEventListener('change', updateSystemTheme);
    return () => media.removeEventListener('change', updateSystemTheme);
  }, [setting]);

  function updateTheme(nextSetting: ThemeSetting) {
    setSetting(nextSetting);
    applyTheme(nextSetting);

    if (nextSetting === 'system') {
      window.localStorage.removeItem('myservicebus-theme');
    } else {
      window.localStorage.setItem('myservicebus-theme', nextSetting);
    }
  }

  return (
    <label className="theme-selector">
      <span className="sr-only">Theme</span>
      <select
        aria-label="Theme"
        onChange={(event) => updateTheme(event.target.value as ThemeSetting)}
        value={setting}
      >
        <option value="system">System</option>
        <option value="light">Light</option>
        <option value="dark">Dark</option>
      </select>
    </label>
  );
}
