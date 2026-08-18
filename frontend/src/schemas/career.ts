import { z } from 'zod';

/**
 * Зеркало `UpdateCareerProfileRequestValidator`.
 *
 * Оба поля необязательны и на бэкенде ограничены только длиной: навыки
 * хранятся строкой (CSV), потому что нормализовать их в отдельную таблицу
 * ради MVP было бы преждевременным усложнением.
 */
export const careerProfileSchema = z.object({
  skills: z.string().max(1000, 'Список навыков не длиннее 1000 символов.'),
  desiredPosition: z.string().max(200, 'Название позиции не длиннее 200 символов.'),
});

export type CareerProfileFormValues = z.infer<typeof careerProfileSchema>;
