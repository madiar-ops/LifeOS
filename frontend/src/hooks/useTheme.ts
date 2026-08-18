import { useContext } from 'react';

import { ThemeContext, type ThemeContextValue } from '@/contexts/theme-context';

/**
 * Доступ к теме.
 *
 * Бросает исключение вне провайдера, а не возвращает значение по умолчанию:
 * молчаливый запасной вариант скрыл бы ошибку сборки дерева компонентов, и
 * переключатель темы просто «не работал» бы без объяснения причины.
 */
export function useTheme(): ThemeContextValue {
  const context = useContext(ThemeContext);
  if (context === null) {
    throw new Error('useTheme вызван вне ThemeProvider.');
  }
  return context;
}
