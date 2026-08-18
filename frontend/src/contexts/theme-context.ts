import { createContext } from 'react';

export type Theme = 'light' | 'dark';

export interface ThemeContextValue {
  theme: Theme;
  toggle: () => void;
  set: (theme: Theme) => void;
}

/**
 * Объект контекста живёт отдельно от провайдера.
 *
 * Файл, который экспортирует и компонент, и контекст, теряет горячую
 * перезагрузку: Vite не может заменить только компонент, потому что контекст —
 * не компонент, и перезагружает модуль целиком вместе с состоянием.
 */
export const ThemeContext = createContext<ThemeContextValue | null>(null);

/** Ключ localStorage. Читается также синхронным скриптом в index.html. */
export const THEME_STORAGE_KEY = 'lifeos.theme';
