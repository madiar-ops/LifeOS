import { describe, expect, it } from 'vitest';

import { messagesForField } from '@/test/zodResult';

import { generateQuizSchema, studyMaterialSchema, studyNoteSchema } from './study';

/**
 * Схемы учебного модуля.
 *
 * Отдельного внимания заслуживает количество вопросов теста: оно уходит в
 * AI-сервис, и дробное либо выходящее за 1..15 значение обернулось бы отказом
 * уже после долгого ожидания генерации.
 */

describe('studyMaterialSchema', () => {
  it('требует название и ограничивает его 200 символами', () => {
    expect(studyMaterialSchema.safeParse({ title: 'Алгоритмы' }).success).toBe(true);
    expect(studyMaterialSchema.safeParse({ title: '' }).success).toBe(false);
    expect(studyMaterialSchema.safeParse({ title: 'т'.repeat(200) }).success).toBe(true);
    expect(studyMaterialSchema.safeParse({ title: 'т'.repeat(201) }).success).toBe(false);
  });
});

describe('studyNoteSchema', () => {
  it('требует текст заметки и ограничивает его 10000 символами', () => {
    expect(studyNoteSchema.safeParse({ content: 'Конспект' }).success).toBe(true);
    expect(studyNoteSchema.safeParse({ content: '' }).success).toBe(false);
    expect(studyNoteSchema.safeParse({ content: 'т'.repeat(10_000) }).success).toBe(true);
    expect(studyNoteSchema.safeParse({ content: 'т'.repeat(10_001) }).success).toBe(false);
  });
});

describe('generateQuizSchema', () => {
  it('принимает границы диапазона 1..15', () => {
    expect(generateQuizSchema.safeParse({ questionCount: 1 }).success).toBe(true);
    expect(generateQuizSchema.safeParse({ questionCount: 15 }).success).toBe(true);
  });

  it('отклоняет значения вне диапазона', () => {
    expect(generateQuizSchema.safeParse({ questionCount: 0 }).success).toBe(false);
    expect(generateQuizSchema.safeParse({ questionCount: 16 }).success).toBe(false);
  });

  it('отклоняет дробное количество вопросов', () => {
    const result = generateQuizSchema.safeParse({ questionCount: 5.5 });
    expect(messagesForField(result, 'questionCount')).toContain(
      'Количество вопросов — целое число.',
    );
  });

  it('отклоняет строку вместо числа', () => {
    expect(generateQuizSchema.safeParse({ questionCount: '5' }).success).toBe(false);
  });
});
