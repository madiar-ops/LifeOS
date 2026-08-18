import { type ClassValue, clsx } from 'clsx';
import { twMerge } from 'tailwind-merge';

/**
 * Склейка классов Tailwind с разрешением конфликтов.
 *
 * `clsx` собирает условные классы, `twMerge` убирает противоречия: в строке
 * «px-3 px-5» победит последний, а не оба — иначе переопределить отступ у
 * компонента снаружи было бы невозможно.
 */
export function cn(...inputs: ClassValue[]): string {
  return twMerge(clsx(inputs));
}
