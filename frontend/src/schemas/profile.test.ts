import { describe, expect, it } from 'vitest';

import { messagesForField } from '@/test/zodResult';

import { changePasswordSchema, profileSchema } from './profile';

/**
 * Схемы профиля и смены пароля.
 *
 * Главное здесь — межполевые правила. Они выполняются `.refine` после проверок
 * отдельных полей, поэтому легко получить ситуацию, когда сообщение приходит
 * не к тому полю: «пароли не совпадают» под полем нового пароля вместо поля
 * подтверждения выглядит как ошибка в самом приложении.
 */

const VALID_CHANGE = {
  currentPassword: 'OldPass1',
  newPassword: 'NewPass1',
  confirmPassword: 'NewPass1',
};

describe('profileSchema', () => {
  it('принимает имя и фамилию', () => {
    expect(profileSchema.safeParse({ name: 'Данияр', surname: 'Абубекеров' }).success).toBe(true);
  });

  it('требует оба поля', () => {
    const result = profileSchema.safeParse({ name: '', surname: '' });
    expect(messagesForField(result, 'name')).toContain('Имя обязательно.');
    expect(messagesForField(result, 'surname')).toContain('Фамилия обязательна.');
  });

  it('ограничивает длину сотней символов', () => {
    expect(profileSchema.safeParse({ name: 'и'.repeat(100), surname: 'А' }).success).toBe(true);
    expect(profileSchema.safeParse({ name: 'и'.repeat(101), surname: 'А' }).success).toBe(false);
  });
});

describe('changePasswordSchema', () => {
  it('принимает корректную смену пароля', () => {
    expect(changePasswordSchema.safeParse(VALID_CHANGE).success).toBe(true);
  });

  it('запрещает новый пароль, совпадающий с текущим', () => {
    const result = changePasswordSchema.safeParse({
      currentPassword: 'SamePass1',
      newPassword: 'SamePass1',
      confirmPassword: 'SamePass1',
    });

    expect(result.success).toBe(false);
    // Сообщение обязано лечь на поле нового пароля — исправлять нужно именно его.
    expect(messagesForField(result, 'newPassword')).toContain(
      'Новый пароль должен отличаться от текущего.',
    );
  });

  it('сообщает о несовпадении подтверждения именно под полем подтверждения', () => {
    const result = changePasswordSchema.safeParse({
      ...VALID_CHANGE,
      confirmPassword: 'NewPass2',
    });

    expect(messagesForField(result, 'confirmPassword')).toContain('Пароли не совпадают.');
    expect(messagesForField(result, 'newPassword')).toEqual([]);
  });

  it('применяет к новому паролю требования сложности', () => {
    const result = changePasswordSchema.safeParse({
      currentPassword: 'OldPass1',
      newPassword: 'simple',
      confirmPassword: 'simple',
    });

    expect(result.success).toBe(false);
    expect(messagesForField(result, 'newPassword')).toContain('Пароль не короче 8 символов.');
  });

  it('требует текущий пароль', () => {
    const result = changePasswordSchema.safeParse({ ...VALID_CHANGE, currentPassword: '' });
    expect(messagesForField(result, 'currentPassword')).toContain('Текущий пароль обязателен.');
  });
});
