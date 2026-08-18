import { z } from 'zod';

/**
 * Зеркало `CreateTaskRequestValidator` / `UpdateTaskRequestValidator`.
 *
 * `goalId` пустая строка означает «задача без цели»: `Tasks.GoalId` на бэкенде
 * необязателен (ADR 6) — задача может существовать сама по себе.
 */
export const taskSchema = z.object({
  title: z.string().min(1, 'Название задачи обязательно.').max(200, 'Не длиннее 200 символов.'),
  goalId: z.string(),
  deadline: z.string(),
  completed: z.boolean(),
});

export type TaskFormValues = z.infer<typeof taskSchema>;
