import { describe, expect, it } from 'vitest';

import { messagesForField } from '@/test/zodResult';

import { transactionSchema } from './transaction';

/**
 * Схема формы транзакции.
 *
 * Здесь важнее всего две границы. Сумма строго больше нуля: транзакция на 0
 * не имеет смысла и портит агрегаты. Верхний предел 999 999 999 стоит на
 * бэкенде (ADR 35) и отсекает опечатку в виде лишнего нуля.
 */

const VALID = {
  type: 'Expense' as const,
  category: 'Продукты',
  amount: 12_500,
  currency: 'KZT',
  date: '2026-08-17',
  description: 'Магазин у дома',
};

describe('transactionSchema', () => {
  it('принимает заполненную транзакцию', () => {
    expect(transactionSchema.safeParse(VALID).success).toBe(true);
  });

  it('отклоняет нулевую и отрицательную сумму', () => {
    expect(transactionSchema.safeParse({ ...VALID, amount: 0 }).success).toBe(false);
    expect(transactionSchema.safeParse({ ...VALID, amount: -100 }).success).toBe(false);
    expect(messagesForField(transactionSchema.safeParse({ ...VALID, amount: 0 }), 'amount')).toContain(
      'Сумма должна быть больше нуля.',
    );
  });

  it('принимает минимальную дробную сумму', () => {
    expect(transactionSchema.safeParse({ ...VALID, amount: 0.01 }).success).toBe(true);
  });

  it('принимает ровно 999 999 999 и отклоняет большее', () => {
    expect(transactionSchema.safeParse({ ...VALID, amount: 999_999_999 }).success).toBe(true);
    expect(transactionSchema.safeParse({ ...VALID, amount: 1_000_000_000 }).success).toBe(false);
  });

  it('принимает код валюты ровно из трёх букв в любом регистре', () => {
    expect(transactionSchema.safeParse({ ...VALID, currency: 'kzt' }).success).toBe(true);
    expect(transactionSchema.safeParse({ ...VALID, currency: 'USD' }).success).toBe(true);
  });

  it('отклоняет код валюты неверной длины или с цифрами', () => {
    for (const currency of ['KZ', 'KZTT', '', '12', 'KZ1', 'KZ ', ' KZT']) {
      expect(transactionSchema.safeParse({ ...VALID, currency }).success).toBe(false);
    }
    expect(
      messagesForField(transactionSchema.safeParse({ ...VALID, currency: 'RU' }), 'currency'),
    ).toContain('Код валюты по ISO 4217: три буквы, например KZT.');
  });

  it('принимает только типы операции из контракта бэкенда', () => {
    expect(transactionSchema.safeParse({ ...VALID, type: 'Income' }).success).toBe(true);
    expect(transactionSchema.safeParse({ ...VALID, type: 'Transfer' }).success).toBe(false);
  });

  it('требует категорию и ограничивает её сотней символов', () => {
    expect(transactionSchema.safeParse({ ...VALID, category: '' }).success).toBe(false);
    expect(transactionSchema.safeParse({ ...VALID, category: 'к'.repeat(100) }).success).toBe(true);
    expect(transactionSchema.safeParse({ ...VALID, category: 'к'.repeat(101) }).success).toBe(false);
  });

  it('требует дату операции', () => {
    expect(transactionSchema.safeParse({ ...VALID, date: '' }).success).toBe(false);
  });

  it('разрешает пустое описание, но не длиннее 500 символов', () => {
    expect(transactionSchema.safeParse({ ...VALID, description: '' }).success).toBe(true);
    expect(transactionSchema.safeParse({ ...VALID, description: 'о'.repeat(500) }).success).toBe(
      true,
    );
    expect(transactionSchema.safeParse({ ...VALID, description: 'о'.repeat(501) }).success).toBe(
      false,
    );
  });
});
