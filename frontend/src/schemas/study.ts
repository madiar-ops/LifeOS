import { z } from 'zod';

/** Зеркало `CreateStudyMaterialRequestValidator`. */
export const studyMaterialSchema = z.object({
  title: z
    .string()
    .min(1, 'Название материала обязательно.')
    .max(200, 'Не длиннее 200 символов.'),
});

/** Зеркало `CreateStudyNoteRequestValidator` / `UpdateStudyNoteRequestValidator`. */
export const studyNoteSchema = z.object({
  content: z
    .string()
    .min(1, 'Текст заметки обязателен.')
    .max(10_000, 'Заметка не длиннее 10000 символов.'),
});

/** Зеркало `GenerateQuizRequestValidator`: 1..15 вопросов. */
export const generateQuizSchema = z.object({
  questionCount: z
    .number({ message: 'Укажи количество вопросов числом.' })
    .int('Количество вопросов — целое число.')
    .min(1, 'От 1 до 15 вопросов.')
    .max(15, 'От 1 до 15 вопросов.'),
});

export type StudyMaterialFormValues = z.infer<typeof studyMaterialSchema>;
export type StudyNoteFormValues = z.infer<typeof studyNoteSchema>;
export type GenerateQuizFormValues = z.infer<typeof generateQuizSchema>;
