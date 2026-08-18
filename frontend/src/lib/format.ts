import { differenceInCalendarDays, format, formatDistanceToNow, parseISO } from 'date-fns';
import { ru } from 'date-fns/locale';

import type { IsoDate, IsoDateTime } from '@/types/api';

/**
 * Форматирование значений для отображения.
 *
 * Всё в одном модуле осознанно: если формат денег или даты определяется в
 * каждом компоненте отдельно, на разных экранах одна и та же сумма выглядит
 * по-разному — классический признак несобранного интерфейса.
 */

const LOCALE = 'ru-RU';

// -------------------------------------------------------------------- Деньги

/**
 * Сумма с валютой.
 *
 * Дробная часть отбрасывается: рабочая валюта проекта — KZT
 * (FinanceService.DefaultCurrency), где копейки не используются. Для валют с
 * дробной частью включаем два знака.
 */
export function formatMoney(amount: number, currency: string): string {
  const fractionless = currency.toUpperCase() === 'KZT';
  try {
    return new Intl.NumberFormat(LOCALE, {
      style: 'currency',
      currency,
      minimumFractionDigits: 0,
      maximumFractionDigits: fractionless ? 0 : 2,
    }).format(amount);
  } catch {
    // Валюта не по ISO 4217 (например, опечатка в старой записи) — Intl бросает.
    return `${formatNumber(amount)} ${currency}`;
  }
}

/** Компактная сумма для плиток: 1 250 000 → «1,3 млн». */
export function formatMoneyCompact(amount: number, currency: string): string {
  try {
    return new Intl.NumberFormat(LOCALE, {
      style: 'currency',
      currency,
      notation: 'compact',
      maximumFractionDigits: 1,
    }).format(amount);
  } catch {
    return formatMoney(amount, currency);
  }
}

export function formatNumber(value: number, maximumFractionDigits = 0): string {
  return new Intl.NumberFormat(LOCALE, { maximumFractionDigits }).format(value);
}

/** Доля 0..1 либо 0..100 → «42 %». */
export function formatPercent(value: number, alreadyPercent = true): string {
  const percent = alreadyPercent ? value : value * 100;
  return `${new Intl.NumberFormat(LOCALE, { maximumFractionDigits: 1 }).format(percent)} %`;
}

// --------------------------------------------------------------------- Даты

/** «2026-08-17» либо ISO-момент → «17 авг 2026». */
export function formatDate(value: IsoDate | IsoDateTime | null | undefined): string {
  if (!value) return '—';
  const parsed = parseISO(value);
  if (Number.isNaN(parsed.getTime())) return '—';
  return format(parsed, 'd MMM yyyy', { locale: ru });
}

export function formatDateTime(value: IsoDateTime | null | undefined): string {
  if (!value) return '—';
  const parsed = parseISO(value);
  if (Number.isNaN(parsed.getTime())) return '—';
  return format(parsed, 'd MMM yyyy, HH:mm', { locale: ru });
}

export function formatRelative(value: IsoDateTime | null | undefined): string {
  if (!value) return '—';
  const parsed = parseISO(value);
  if (Number.isNaN(parsed.getTime())) return '—';
  return formatDistanceToNow(parsed, { locale: ru, addSuffix: true });
}

/** «2026-08» (MonthlyPoint.month с бэкенда) → «авг 2026». */
export function formatMonth(month: string): string {
  const parsed = parseISO(`${month}-01`);
  if (Number.isNaN(parsed.getTime())) return month;
  return format(parsed, 'LLL yyyy', { locale: ru });
}

/** Короткая подпись оси графика: «2026-08» → «авг». */
export function formatMonthShort(month: string): string {
  const parsed = parseISO(`${month}-01`);
  if (Number.isNaN(parsed.getTime())) return month;
  return format(parsed, 'LLL', { locale: ru });
}

/** Подпись оси для дневных точек: «2026-08-17» → «17.08». */
export function formatDayShort(value: IsoDate): string {
  const parsed = parseISO(value);
  if (Number.isNaN(parsed.getTime())) return value;
  return format(parsed, 'dd.MM');
}

/** Сегодняшняя дата в формате C# DateOnly. */
export function todayIsoDate(): IsoDate {
  return format(new Date(), 'yyyy-MM-dd');
}

/** Дата N дней назад в формате DateOnly — для фильтров «за период». */
export function isoDateDaysAgo(days: number): IsoDate {
  const date = new Date();
  date.setDate(date.getDate() - days);
  return format(date, 'yyyy-MM-dd');
}

/** Первый день текущего месяца. */
export function firstDayOfMonthIso(): IsoDate {
  return format(new Date(new Date().getFullYear(), new Date().getMonth(), 1), 'yyyy-MM-dd');
}

// --------------------------------------------- Мост к полям ввода браузера

/**
 * ISO-момент → значение для `<input type="datetime-local">`.
 *
 * Поле ввода работает в ЛОКАЛЬНОМ времени и не принимает суффикс «Z».
 * Без явного преобразования пользователь в UTC+5 увидел бы дедлайн,
 * сдвинутый на пять часов.
 */
export function isoToDatetimeLocal(value: IsoDateTime | null | undefined): string {
  if (!value) return '';
  const parsed = parseISO(value);
  if (Number.isNaN(parsed.getTime())) return '';
  return format(parsed, "yyyy-MM-dd'T'HH:mm");
}

/** Значение `<input type="datetime-local">` → ISO-момент в UTC для бэкенда. */
export function datetimeLocalToIso(value: string): IsoDateTime | null {
  if (!value) return null;
  const parsed = new Date(value);
  if (Number.isNaN(parsed.getTime())) return null;
  return parsed.toISOString();
}

// -------------------------------------------------------------------- Прочее

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${String(bytes)} Б`;
  const units = ['КБ', 'МБ', 'ГБ'];
  let value = bytes / 1024;
  let unitIndex = 0;
  while (value >= 1024 && unitIndex < units.length - 1) {
    value /= 1024;
    unitIndex += 1;
  }
  return `${value.toFixed(value < 10 ? 1 : 0)} ${units[unitIndex] ?? 'ГБ'}`;
}

export type DeadlineState = 'none' | 'overdue' | 'today' | 'soon' | 'later';

/**
 * Состояние дедлайна — для цветовой подсветки.
 *
 * Возвращает состояние, а не готовый CSS-класс: решение о цвете принимает
 * компонент, логика «просрочено ли» остаётся одна на всё приложение.
 */
export function deadlineState(value: IsoDateTime | null | undefined): DeadlineState {
  if (!value) return 'none';
  const parsed = parseISO(value);
  if (Number.isNaN(parsed.getTime())) return 'none';
  const days = differenceInCalendarDays(parsed, new Date());
  if (days < 0) return 'overdue';
  if (days === 0) return 'today';
  if (days <= 3) return 'soon';
  return 'later';
}

/** Инициалы для аватара-заглушки. */
export function initials(name: string, surname: string): string {
  return `${name.charAt(0)}${surname.charAt(0)}`.toUpperCase() || '?';
}
