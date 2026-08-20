import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import {
  datetimeLocalToIso,
  deadlineState,
  firstDayOfMonthIso,
  formatDate,
  formatDateTime,
  formatDayShort,
  formatFileSize,
  formatMoney,
  formatMoneyCompact,
  formatMonth,
  formatMonthShort,
  formatNumber,
  formatPercent,
  formatRelative,
  initials,
  isoDateDaysAgo,
  isoToDatetimeLocal,
  todayIsoDate,
} from './format';

/**
 * Тесты форматирования.
 *
 * Часовой пояс тестов зафиксирован в vite.config.ts (`Asia/Almaty`, UTC+5) —
 * иначе проверки дат зависели бы от настроек машины. Пробелы в результатах
 * Intl нормализуются: разделитель разрядов там неразрывный, а в разных версиях
 * ICU это то U+00A0, то U+202F, и сравнение по точной строке ломалось бы при
 * обновлении Node.
 */

function normalizeSpaces(value: string): string {
  return value.replace(/\s/gu, ' ');
}

describe('formatMoney', () => {
  it('не показывает копейки для KZT — рабочей валюты проекта', () => {
    expect(normalizeSpaces(formatMoney(12_500.49, 'KZT'))).toMatch(/^12 500 (KZT|₸)$/u);
  });

  it('показывает дробную часть для валют, где она используется', () => {
    expect(normalizeSpaces(formatMoney(1234.5, 'USD'))).toContain('1 234,5');
  });

  it('не теряет знак у отрицательной суммы', () => {
    expect(formatMoney(-500, 'KZT')).toContain('-');
  });

  it('не падает на коде валюты вне ISO 4217, а показывает его как есть', () => {
    // В старых записях мог остаться неверный код: Intl на нём бросает
    // RangeError, и без запасного пути сломался бы весь список транзакций.
    expect(normalizeSpaces(formatMoney(1000, 'ZZ'))).toBe('1 000 ZZ');
  });
});

describe('formatMoneyCompact', () => {
  it('сокращает миллионы для плиток дашборда', () => {
    expect(normalizeSpaces(formatMoneyCompact(1_250_000, 'KZT'))).toContain('1,3 млн');
  });
});

describe('formatNumber и formatPercent', () => {
  it('разделяет разряды', () => {
    expect(normalizeSpaces(formatNumber(1_234_567))).toBe('1 234 567');
  });

  it('округляет проценты до одного знака', () => {
    expect(formatPercent(42.35)).toBe('42,4 %');
  });

  it('переводит долю в проценты по явному флагу', () => {
    expect(formatPercent(0.42, false)).toBe('42 %');
  });
});

describe('форматирование дат', () => {
  it('показывает дату в коротком русском формате', () => {
    expect(formatDate('2026-08-17')).toBe('17 авг. 2026');
  });

  it('переводит момент UTC в местное время (UTC+5)', () => {
    expect(formatDateTime('2026-08-17T14:30:00Z')).toBe('17 авг. 2026, 19:30');
  });

  it('подставляет прочерк вместо пустого значения', () => {
    // Пустая ячейка выглядит как сбой отрисовки; прочерк однозначно читается
    // как «данных нет».
    expect(formatDate(null)).toBe('—');
    expect(formatDate(undefined)).toBe('—');
    expect(formatDateTime(null)).toBe('—');
    expect(formatRelative(null)).toBe('—');
  });

  it('не бросает исключение на испорченной строке', () => {
    expect(formatDate('не дата')).toBe('—');
    expect(formatDateTime('2026-13-45')).toBe('—');
  });

  it('форматирует месяц из MonthlyPoint бэкенда', () => {
    expect(formatMonth('2026-08')).toBe('авг. 2026');
    expect(formatMonthShort('2026-08')).toBe('авг.');
    // Неразобранное значение возвращается как есть — подпись оси не пропадёт.
    expect(formatMonth('мусор')).toBe('мусор');
  });

  it('форматирует подпись дневной точки графика', () => {
    expect(formatDayShort('2026-08-17')).toBe('17.08');
  });

  it('строит относительную подпись по-русски', () => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-17T12:00:00Z'));
    expect(formatRelative('2026-08-17T10:00:00Z')).toBe('около 2 часов назад');
    vi.useRealTimers();
  });
});

describe('даты для запросов к API', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    // 17 августа 2026, 20:00 по Алматы — намеренно поздний вечер: при
    // вычислении даты через UTC результат уехал бы на сутки назад.
    vi.setSystemTime(new Date('2026-08-17T15:00:00Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('возвращает сегодняшнюю дату в формате DateOnly', () => {
    expect(todayIsoDate()).toBe('2026-08-17');
  });

  it('отсчитывает дни назад с учётом перехода через месяц', () => {
    expect(isoDateDaysAgo(30)).toBe('2026-07-18');
    expect(isoDateDaysAgo(0)).toBe('2026-08-17');
  });

  it('возвращает первый день текущего месяца', () => {
    expect(firstDayOfMonthIso()).toBe('2026-08-01');
  });
});

describe('мост к полю datetime-local', () => {
  it('переводит момент UTC в местное время без суффикса Z', () => {
    // Поле ввода работает в местном времени и не принимает «Z»: без сдвига
    // пользователь в UTC+5 увидел бы дедлайн на пять часов раньше.
    expect(isoToDatetimeLocal('2026-08-17T05:00:00Z')).toBe('2026-08-17T10:00');
  });

  it('переводит значение поля обратно в UTC', () => {
    expect(datetimeLocalToIso('2026-08-17T10:00')).toBe('2026-08-17T05:00:00.000Z');
  });

  it('сохраняет момент при обороте туда и обратно', () => {
    const iso = '2026-12-31T18:45:00.000Z';
    expect(datetimeLocalToIso(isoToDatetimeLocal(iso))).toBe(iso);
  });

  it('трактует пустое поле как отсутствие срока, а не как ошибку', () => {
    expect(isoToDatetimeLocal(null)).toBe('');
    expect(isoToDatetimeLocal('')).toBe('');
    expect(datetimeLocalToIso('')).toBeNull();
    expect(datetimeLocalToIso('не дата')).toBeNull();
  });
});

describe('formatFileSize', () => {
  it('оставляет байты без пересчёта до килобайта', () => {
    expect(formatFileSize(0)).toBe('0 Б');
    expect(formatFileSize(1023)).toBe('1023 Б');
  });

  it('переходит к следующей единице ровно на 1024', () => {
    expect(formatFileSize(1024)).toBe('1.0 КБ');
    expect(formatFileSize(1536)).toBe('1.5 КБ');
    expect(formatFileSize(1024 * 1024)).toBe('1.0 МБ');
    expect(formatFileSize(1024 ** 3)).toBe('1.0 ГБ');
  });

  it('убирает дробную часть, когда число уже читается', () => {
    expect(formatFileSize(10 * 1024 * 1024)).toBe('10 МБ');
  });

  it('не поднимается выше гигабайтов', () => {
    // Ограничение на размер файла на бэкенде измеряется мегабайтами, поэтому
    // терабайты не нужны — но и «1024.0 ГБ» вместо мусора тоже приемлемо.
    expect(formatFileSize(2048 * 1024 ** 3)).toContain('ГБ');
  });
});

describe('deadlineState', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-08-17T12:00:00+05:00'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('возвращает none, если срока нет или он не разобрался', () => {
    expect(deadlineState(null)).toBe('none');
    expect(deadlineState(undefined)).toBe('none');
    expect(deadlineState('не дата')).toBe('none');
  });

  it('различает просрочку, сегодня, ближайшие дни и далёкий срок', () => {
    expect(deadlineState('2026-08-16T23:59:00+05:00')).toBe('overdue');
    // «Сегодня» считается по календарным дням, а не по 24 часам: срок в 09:00
    // при текущем времени 12:00 того же дня — просрочка, но не «вчера».
    expect(deadlineState('2026-08-17T09:00:00+05:00')).toBe('today');
    expect(deadlineState('2026-08-17T23:00:00+05:00')).toBe('today');
    expect(deadlineState('2026-08-18T01:00:00+05:00')).toBe('soon');
    expect(deadlineState('2026-08-20T23:00:00+05:00')).toBe('soon');
    expect(deadlineState('2026-08-21T00:30:00+05:00')).toBe('later');
  });
});

describe('initials', () => {
  it('берёт по первой букве имени и фамилии в верхнем регистре', () => {
    expect(initials('данияр', 'абубекеров')).toBe('ДА');
  });

  it('не падает на пустых значениях', () => {
    expect(initials('', '')).toBe('?');
  });
});
