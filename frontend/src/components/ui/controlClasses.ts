import { cn } from '@/lib/cn';

/**
 * Общие классы для всех полей ввода: input, select, textarea.
 *
 * Вынесено в отдельный модуль, а не оставлено рядом с компонентом Input:
 * файл, экспортирующий и компонент, и константу, ломает горячую перезагрузку
 * Vite — при правке константы модуль перезагружается целиком и состояние
 * формы теряется.
 */
export const controlClasses = cn(
  'w-full rounded-lg border border-line bg-surface px-3 text-sm text-fg',
  'placeholder:text-fg-subtle',
  'transition-[border-color,box-shadow] duration-150',
  'hover:border-line-strong',
  'focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/25',
  'disabled:cursor-not-allowed disabled:bg-surface-2 disabled:text-fg-subtle',
  'aria-invalid:border-danger aria-invalid:focus:ring-danger/25',
);
