import { z } from 'zod';

import { TRANSACTION_TYPE_VALUES } from '@/types/enums';

/**
 * Зеркало `CreateTransactionRequestValidator`.
 *
 * Обрати внимание на границу 999 999 999: она не выдумана здесь, а стоит на
 * бэкенде и отсекает опечатку вида лишнего нуля. Верхний предел важнее, чем
 * кажется, — с ним сумма гарантированно остаётся в безопасном диапазоне
 * JavaScript-числа и не теряет точность по дороге.
 *
 * Валюта проверяется регулярным выражением ^[A-Za-z]{3}$ — тем же, что в
 * FluentValidation. Значение приводится к верхнему регистру при отправке.
 */
export const transactionSchema = z.object({
  type: z.enum(TRANSACTION_TYPE_VALUES),
  category: z.string().min(1, 'Категория обязательна.').max(100, 'Не длиннее 100 символов.'),
  amount: z
    .number({ message: 'Введи сумму числом.' })
    .positive('Сумма должна быть больше нуля.')
    .max(999_999_999, 'Сумма слишком велика.'),
  currency: z
    .string()
    .regex(/^[A-Za-z]{3}$/, 'Код валюты по ISO 4217: три буквы, например KZT.'),
  date: z.string().min(1, 'Дата операции обязательна.'),
  description: z.string().max(500, 'Описание не длиннее 500 символов.'),
});

export type TransactionFormValues = z.infer<typeof transactionSchema>;
