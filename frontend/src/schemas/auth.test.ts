import { describe, expect, it } from 'vitest';

import { messagesOf } from '@/test/zodResult';

import { loginSchema, passwordSchema, registerSchema } from './auth';

/**
 * Схемы форм аутентификации.
 *
 * Проверяются ГРАНИЦЫ, а не середина диапазона: 7 и 8 символов, а не 3 и 20.
 * Ошибка в правиле почти всегда сидит именно на границе — со «сдвигом на
 * единицу» либо с потерянным требованием к составу символов. Схема зеркалит
 * `RegisterRequestValidator` на бэкенде, поэтому расхождение здесь означает,
 * что пользователь получит отказ сервера после отправки формы.
 */

const VALID = 'Password1';

describe('passwordSchema', () => {
  it('принимает пароль ровно в 8 символов с заглавной, строчной и цифрой', () => {
    const result = passwordSchema.safeParse(VALID);
    expect(result.success).toBe(true);
  });

  it('отклоняет пароль в 7 символов', () => {
    const result = passwordSchema.safeParse('Passwo1');
    expect(result.success).toBe(false);
    expect(messagesOf(result)).toContain('Пароль не короче 8 символов.');
  });

  it('принимает пароль ровно в 128 символов и отклоняет 129', () => {
    const filler = 'a'.repeat(126);
    expect(passwordSchema.safeParse(`A1${filler}`).success).toBe(true);
    expect(passwordSchema.safeParse(`A1${filler}a`).success).toBe(false);
  });

  it('требует заглавную букву', () => {
    expect(messagesOf(passwordSchema.safeParse('password1'))).toContain(
      'Нужна хотя бы одна заглавная буква.',
    );
  });

  it('требует строчную букву', () => {
    expect(messagesOf(passwordSchema.safeParse('PASSWORD1'))).toContain(
      'Нужна хотя бы одна строчная буква.',
    );
  });

  it('требует цифру', () => {
    expect(messagesOf(passwordSchema.safeParse('PasswordX'))).toContain(
      'Нужна хотя бы одна цифра.',
    );
  });

  it('не считает кириллицу заменой латинской заглавной букве', () => {
    // Требование на бэкенде описано классами [A-Z]/[a-z], и клиент обязан
    // трактовать его так же — иначе форма пропустит пароль, который отвергнет
    // сервер, и пользователь не поймёт причину отказа.
    expect(passwordSchema.safeParse('Пароль12').success).toBe(false);
  });
});

describe('loginSchema', () => {
  it('принимает корректную пару email и пароля', () => {
    expect(loginSchema.safeParse({ email: 'user@example.com', password: 'x' }).success).toBe(true);
  });

  it('не проверяет сложность пароля на входе', () => {
    // Правила сложности могли измениться после регистрации; сообщение
    // «пароль слишком простой» на форме входа было бы бессмысленным.
    expect(loginSchema.safeParse({ email: 'user@example.com', password: '1' }).success).toBe(true);
  });

  it('отклоняет строку без символа @', () => {
    const result = loginSchema.safeParse({ email: 'user.example.com', password: 'x' });
    expect(result.success).toBe(false);
    expect(messagesOf(result)).toContain('Некорректный формат email.');
  });

  it('отклоняет пустые поля', () => {
    const result = loginSchema.safeParse({ email: '', password: '' });
    expect(messagesOf(result)).toContain('Email обязателен.');
    expect(messagesOf(result)).toContain('Пароль обязателен.');
  });
});

describe('registerSchema', () => {
  const valid = {
    name: 'Данияр',
    surname: 'Абубекеров',
    email: 'user@example.com',
    password: VALID,
  };

  it('принимает заполненную форму регистрации', () => {
    expect(registerSchema.safeParse(valid).success).toBe(true);
  });

  it('ограничивает имя и фамилию сотней символов', () => {
    expect(registerSchema.safeParse({ ...valid, name: 'и'.repeat(100) }).success).toBe(true);
    expect(registerSchema.safeParse({ ...valid, name: 'и'.repeat(101) }).success).toBe(false);
    expect(registerSchema.safeParse({ ...valid, surname: 'и'.repeat(101) }).success).toBe(false);
  });

  it('ограничивает email 256 символами — как колонка в базе', () => {
    const local = 'a'.repeat(256 - '@example.com'.length);
    expect(registerSchema.safeParse({ ...valid, email: `${local}@example.com` }).success).toBe(true);
    expect(registerSchema.safeParse({ ...valid, email: `a${local}@example.com` }).success).toBe(
      false,
    );
  });

  it('применяет к паролю полные требования сложности', () => {
    const result = registerSchema.safeParse({ ...valid, password: 'qwerty' });
    expect(result.success).toBe(false);
    expect(messagesOf(result)).toContain('Пароль не короче 8 символов.');
  });
});
