import { useMemo } from 'react';

import { useTheme } from '@/hooks/useTheme';

export interface ChartTheme {
  grid: string;
  axis: string;
  tooltipBg: string;
  tooltipBorder: string;
  tooltipText: string;
  income: string;
  expense: string;
  accent: string;
  /** Палитра для категорий: до шести различимых оттенков. */
  categories: string[];
}

/**
 * Цвета для графиков.
 *
 * Recharts принимает только конкретные значения цвета — CSS-классы Tailwind ему
 * не подходят. Поэтому палитра здесь задана явно и переключается по теме.
 *
 * Читать переменные через `getComputedStyle` было бы «честнее», но это
 * заставляет графики перерисовываться на каждый кадр анимации темы и
 * возвращает пустые строки при первом рендере, до применения стилей.
 *
 * Оттенки категорий подобраны так, чтобы различаться не только цветом, но и
 * светлотой: график остаётся читаемым при чёрно-белой печати и для
 * пользователей с дальтонизмом.
 */
export function useChartTheme(): ChartTheme {
  const { theme } = useTheme();

  return useMemo<ChartTheme>(() => {
    if (theme === 'dark') {
      return {
        grid: '#26262c',
        axis: '#6e6e78',
        tooltipBg: '#16161a',
        tooltipBorder: '#3a3a43',
        tooltipText: '#f4f4f5',
        income: '#34d399',
        expense: '#fb7185',
        accent: '#7c7cf0',
        categories: ['#7c7cf0', '#34d399', '#fbbf24', '#38bdf8', '#f472b6', '#a3a3ad'],
      };
    }

    return {
      grid: '#e6e6e9',
      axis: '#9b9ba4',
      tooltipBg: '#ffffff',
      tooltipBorder: '#d4d4d8',
      tooltipText: '#18181b',
      income: '#0d9f6e',
      expense: '#dc3a52',
      accent: '#5b5bd6',
      categories: ['#5b5bd6', '#0d9f6e', '#c2810c', '#0d80c2', '#c2456f', '#8b8b95'],
    };
  }, [theme]);
}
