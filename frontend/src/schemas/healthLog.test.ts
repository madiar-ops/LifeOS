import { describe, expect, it } from 'vitest';

import { messagesForField } from '@/test/zodResult';

import { healthLogSchema } from './healthLog';

/**
 * Схема записи о здоровье.
 *
 * Диапазоны зеркалят `CreateHealthLogRequestValidator`. Проверяются обе границы
 * каждого диапазона: включающая граница, ошибочно сделанная исключающей,
 * запретила бы совершенно нормальные значения — например ровно 8 часов сна.
 */

const VALID = {
  date: '2026-08-17',
  weight: 72.5,
  sleepHours: 7.5,
  mood: 'Good' as const,
  waterMl: 2000,
  steps: 8500,
};

describe('healthLogSchema', () => {
  it('принимает заполненную запись', () => {
    expect(healthLogSchema.safeParse(VALID).success).toBe(true);
  });

  it('требует дату записи', () => {
    expect(messagesForField(healthLogSchema.safeParse({ ...VALID, date: '' }), 'date')).toContain(
      'Дата записи обязательна.',
    );
  });

  it('принимает вес на границах 20 и 400 кг', () => {
    expect(healthLogSchema.safeParse({ ...VALID, weight: 20 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, weight: 400 }).success).toBe(true);
  });

  it('отклоняет вес вне диапазона 20–400 кг', () => {
    expect(healthLogSchema.safeParse({ ...VALID, weight: 19.9 }).success).toBe(false);
    expect(healthLogSchema.safeParse({ ...VALID, weight: 400.1 }).success).toBe(false);
    expect(messagesForField(healthLogSchema.safeParse({ ...VALID, weight: 700 }), 'weight')).toContain(
      'Вес должен быть в диапазоне 20–400 кг.',
    );
  });

  it('принимает сон на границах 0 и 24 часов', () => {
    expect(healthLogSchema.safeParse({ ...VALID, sleepHours: 0 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, sleepHours: 24 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, sleepHours: 24.5 }).success).toBe(false);
    expect(healthLogSchema.safeParse({ ...VALID, sleepHours: -1 }).success).toBe(false);
  });

  it('различает «не измерял» и нулевое значение', () => {
    // null означает отсутствие измерения. Подмена его нулём испортила бы
    // обучающую выборку: «спал 0 часов» и «не записал сон» — разные факты.
    expect(healthLogSchema.safeParse({ ...VALID, weight: null, sleepHours: null }).success).toBe(
      true,
    );
  });

  it('не принимает строку вместо числа в измерениях', () => {
    // Поле ввода отдаёт строку, и молчаливое приведение «» → 0 записало бы
    // в базу выдуманный ноль. Схема обязана требовать именно число.
    expect(healthLogSchema.safeParse({ ...VALID, weight: '72.5' }).success).toBe(false);
    expect(healthLogSchema.safeParse({ ...VALID, steps: '8500' }).success).toBe(false);
  });

  it('принимает объём воды на границах 0 и 20000 мл', () => {
    expect(healthLogSchema.safeParse({ ...VALID, waterMl: 0 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, waterMl: 20_000 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, waterMl: 20_001 }).success).toBe(false);
    expect(healthLogSchema.safeParse({ ...VALID, waterMl: -1 }).success).toBe(false);
  });

  it('принимает шаги на границах 0 и 200000', () => {
    expect(healthLogSchema.safeParse({ ...VALID, steps: 0 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, steps: 200_000 }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, steps: 200_001 }).success).toBe(false);
  });

  it('вода и шаги обязательны — их нельзя не указать', () => {
    expect(healthLogSchema.safeParse({ ...VALID, waterMl: null }).success).toBe(false);
    expect(healthLogSchema.safeParse({ ...VALID, steps: null }).success).toBe(false);
  });

  it('принимает только значения настроения из контракта бэкенда', () => {
    expect(healthLogSchema.safeParse({ ...VALID, mood: 'VeryGood' }).success).toBe(true);
    expect(healthLogSchema.safeParse({ ...VALID, mood: 'Excellent' }).success).toBe(false);
    // Сериализация enum'ов строками чувствительна к регистру.
    expect(healthLogSchema.safeParse({ ...VALID, mood: 'good' }).success).toBe(false);
  });
});
