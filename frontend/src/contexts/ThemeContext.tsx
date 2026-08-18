import { useCallback, useEffect, useMemo, useState, type ReactNode } from 'react';

import { THEME_STORAGE_KEY, ThemeContext, type Theme, type ThemeContextValue } from './theme-context';

/** То же значение, что вычисляет синхронный скрипт в index.html. */
function readInitialTheme(): Theme {
  try {
    const saved = window.localStorage.getItem(THEME_STORAGE_KEY);
    if (saved === 'light' || saved === 'dark') return saved;
  } catch {
    /* приватный режим блокирует localStorage */
  }
  return window.matchMedia('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

/**
 * Тема оформления.
 *
 * Это ровно та задача, для которой Context и создан: значение нужно всему
 * дереву, меняется редко и не имеет отношения к серверу. Кэш, инвалидация и
 * повторы ему не нужны — поэтому здесь Context, а не TanStack Query.
 *
 * Первое применение класса делает синхронный скрипт в index.html, до отрисовки
 * React. Этот провайдер лишь поддерживает состояние синхронным после
 * переключения.
 */
export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(readInitialTheme);

  useEffect(() => {
    document.documentElement.classList.toggle('dark', theme === 'dark');
    document.documentElement.style.colorScheme = theme;
    try {
      window.localStorage.setItem(THEME_STORAGE_KEY, theme);
    } catch {
      /* приватный режим: тема просто не запомнится между сессиями */
    }
  }, [theme]);

  const set = useCallback((next: Theme) => setTheme(next), []);
  const toggle = useCallback(
    () => setTheme((current) => (current === 'dark' ? 'light' : 'dark')),
    [],
  );

  const value = useMemo<ThemeContextValue>(() => ({ theme, toggle, set }), [theme, toggle, set]);

  return <ThemeContext.Provider value={value}>{children}</ThemeContext.Provider>;
}
