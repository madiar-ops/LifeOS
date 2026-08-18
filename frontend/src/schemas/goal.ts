import { z } from 'zod';

import { GOAL_STATUS_VALUES, PRIORITY_VALUES } from '@/types/enums';

/**
 * Зеркало `CreateGoalRequestValidator` / `UpdateGoalRequestValidator`.
 *
 * Значения перечислений берутся из тех же массивов, что и выпадающие списки:
 * добавили статус на бэкенде → поправили union в types/enums.ts → схема и
 * список обновились сами. Захардкоженный список строк здесь разошёлся бы с
 * интерфейсом при первом же изменении домена.
 *
 * Дедлайн — строка из `<input type="datetime-local">`, поэтому пустое значение
 * это '' , а не null. Преобразование в ISO-момент делает вызывающий код через
 * `datetimeLocalToIso` — схема не должна знать про формат транспорта.
 */
export const goalSchema = z.object({
  title: z.string().min(1, 'Название цели обязательно.').max(200, 'Не длиннее 200 символов.'),
  description: z.string().max(2000, 'Описание не длиннее 2000 символов.'),
  status: z.enum(GOAL_STATUS_VALUES),
  priority: z.enum(PRIORITY_VALUES),
  deadline: z.string(),
});

export type GoalFormValues = z.infer<typeof goalSchema>;
