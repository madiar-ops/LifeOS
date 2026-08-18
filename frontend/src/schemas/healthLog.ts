import { z } from 'zod';

import { MOOD_VALUES } from '@/types/enums';

/**
 * Зеркало `CreateHealthLogRequestValidator` / `UpdateHealthLogRequestValidator`.
 *
 * Диапазоны взяты с бэкенда без изменений: вес 20–400 кг, сон 0–24 ч,
 * вода 0–20 000 мл, шаги 0–200 000. Это не придирки, а отсечение опечаток:
 * «700» в поле веса и «200000» в поле шагов — почти всегда лишний ноль.
 *
 * Вес и сон необязательны (`decimal?` на бэкенде), поэтому в форме это
 * `number | null`: пустое поле должно означать «не измерял», а не ноль.
 * Ноль килограммов и ноль часов сна — разные утверждения, чем отсутствие
 * данных, и подмена одного другим испортила бы обучающую выборку для AI.
 */
const optionalMeasurement = (min: number, max: number, message: string) =>
  z
    .number({ message: 'Введи значение числом.' })
    .min(min, message)
    .max(max, message)
    .nullable();

export const healthLogSchema = z.object({
  date: z.string().min(1, 'Дата записи обязательна.'),
  weight: optionalMeasurement(20, 400, 'Вес должен быть в диапазоне 20–400 кг.'),
  sleepHours: optionalMeasurement(0, 24, 'Сон не может превышать 24 часа в сутки.'),
  mood: z.enum(MOOD_VALUES),
  waterMl: z
    .number({ message: 'Введи объём числом.' })
    .min(0, 'Объём воды 0–20000 мл.')
    .max(20_000, 'Объём воды 0–20000 мл.'),
  steps: z
    .number({ message: 'Введи количество шагов числом.' })
    .min(0, 'Шаги 0–200000.')
    .max(200_000, 'Шаги 0–200000.'),
});

export type HealthLogFormValues = z.infer<typeof healthLogSchema>;
