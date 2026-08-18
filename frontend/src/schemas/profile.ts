import { z } from 'zod';

import { passwordSchema } from './auth';

/** Зеркало `UpdateProfileRequestValidator`. */
export const profileSchema = z.object({
  name: z.string().min(1, 'Имя обязательно.').max(100, 'Имя не длиннее 100 символов.'),
  surname: z.string().min(1, 'Фамилия обязательна.').max(100, 'Фамилия не длиннее 100 символов.'),
});

/**
 * Зеркало `ChangePasswordRequestValidator`.
 *
 * Правило «новый пароль отличается от текущего» на бэкенде выражено через
 * `NotEqual(x => x.CurrentPassword)`. В zod межполевые проверки живут в
 * `.refine` — она выполняется после проверок отдельных полей.
 *
 * Поле подтверждения существует только на клиенте: бэкенду второй экземпляр
 * пароля не нужен, он защищает от опечатки в интерфейсе.
 */
export const changePasswordSchema = z
  .object({
    currentPassword: z.string().min(1, 'Текущий пароль обязателен.'),
    newPassword: passwordSchema,
    confirmPassword: z.string().min(1, 'Повтори новый пароль.'),
  })
  .refine((values) => values.newPassword !== values.currentPassword, {
    message: 'Новый пароль должен отличаться от текущего.',
    path: ['newPassword'],
  })
  .refine((values) => values.newPassword === values.confirmPassword, {
    message: 'Пароли не совпадают.',
    path: ['confirmPassword'],
  });

export type ProfileFormValues = z.infer<typeof profileSchema>;
export type ChangePasswordFormValues = z.infer<typeof changePasswordSchema>;
