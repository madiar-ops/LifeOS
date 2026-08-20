import { describe, expect, it } from 'vitest';

import { GOAL_STATUS_VALUES, PRIORITY_VALUES } from '@/types/enums';

import { goalSchema } from './goal';

/**
 * Схема формы цели.
 *
 * Отдельная проверка на то, что перечисления берутся из `types/enums.ts`, а не
 * продублированы строками: расхождение списка в схеме и в выпадающем списке
 * даёт форму, которая не отправляется при выборе внешне допустимого значения.
 */

const VALID = {
  title: 'Сдать диплом',
  description: 'Собрать все фазы проекта',
  status: 'InProgress' as const,
  priority: 'High' as const,
  deadline: '2026-09-01T10:00',
};

describe('goalSchema', () => {
  it('принимает заполненную цель', () => {
    expect(goalSchema.safeParse(VALID).success).toBe(true);
  });

  it('принимает каждое значение статуса и приоритета из контракта', () => {
    for (const status of GOAL_STATUS_VALUES) {
      expect(goalSchema.safeParse({ ...VALID, status }).success).toBe(true);
    }
    for (const priority of PRIORITY_VALUES) {
      expect(goalSchema.safeParse({ ...VALID, priority }).success).toBe(true);
    }
  });

  it('отклоняет статус, которого нет на бэкенде', () => {
    expect(goalSchema.safeParse({ ...VALID, status: 'Paused' }).success).toBe(false);
  });

  it('требует название и ограничивает его 200 символами', () => {
    expect(goalSchema.safeParse({ ...VALID, title: '' }).success).toBe(false);
    expect(goalSchema.safeParse({ ...VALID, title: 'т'.repeat(200) }).success).toBe(true);
    expect(goalSchema.safeParse({ ...VALID, title: 'т'.repeat(201) }).success).toBe(false);
  });

  it('разрешает пустой дедлайн: цель без срока — обычное дело', () => {
    expect(goalSchema.safeParse({ ...VALID, deadline: '' }).success).toBe(true);
  });

  it('ограничивает описание 2000 символами', () => {
    expect(goalSchema.safeParse({ ...VALID, description: 'о'.repeat(2000) }).success).toBe(true);
    expect(goalSchema.safeParse({ ...VALID, description: 'о'.repeat(2001) }).success).toBe(false);
  });
});
