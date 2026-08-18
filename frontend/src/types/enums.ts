/**
 * Перечисления домена.
 *
 * На бэкенде в `Program.cs` зарегистрирован `JsonStringEnumConverter`, поэтому
 * по сети enum'ы ходят СТРОКАМИ («InProgress», а не 1).
 *
 * Здесь они объявлены union-типами строк, а НЕ через `enum` TypeScript.
 * Причина: `enum` создаёт объект в рантайме и требует приведения типа при
 * получении строки из JSON (`json.status as GoalStatus`) — то есть ровно в той
 * точке, где типизация и нужна, она отключается. Union-тип строк проверяется
 * компилятором и совпадает с контрактом сервера один к одному.
 *
 * Массивы `*_VALUES` дают единственный источник правды для выпадающих списков:
 * добавление значения в union немедленно ломает сборку, если забыли метку.
 */

// ---------------------------------------------------------------- UserRole

export type UserRole = 'User' | 'Admin';

// -------------------------------------------------------------- GoalStatus

export type GoalStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Cancelled';

export const GOAL_STATUS_VALUES = [
  'NotStarted',
  'InProgress',
  'Completed',
  'Cancelled',
] as const satisfies readonly GoalStatus[];

export const GOAL_STATUS_LABELS: Record<GoalStatus, string> = {
  NotStarted: 'Не начата',
  InProgress: 'В работе',
  Completed: 'Завершена',
  Cancelled: 'Отменена',
};

// ----------------------------------------------------------- PriorityLevel

export type PriorityLevel = 'Low' | 'Medium' | 'High';

export const PRIORITY_VALUES = [
  'Low',
  'Medium',
  'High',
] as const satisfies readonly PriorityLevel[];

export const PRIORITY_LABELS: Record<PriorityLevel, string> = {
  Low: 'Низкий',
  Medium: 'Средний',
  High: 'Высокий',
};

// --------------------------------------------------------- TransactionType

export type TransactionType = 'Income' | 'Expense';

export const TRANSACTION_TYPE_VALUES = [
  'Income',
  'Expense',
] as const satisfies readonly TransactionType[];

export const TRANSACTION_TYPE_LABELS: Record<TransactionType, string> = {
  Income: 'Доход',
  Expense: 'Расход',
};

// --------------------------------------------------------------- MoodLevel

export type MoodLevel = 'VeryBad' | 'Bad' | 'Neutral' | 'Good' | 'VeryGood';

export const MOOD_VALUES = [
  'VeryBad',
  'Bad',
  'Neutral',
  'Good',
  'VeryGood',
] as const satisfies readonly MoodLevel[];

export const MOOD_LABELS: Record<MoodLevel, string> = {
  VeryBad: 'Очень плохо',
  Bad: 'Плохо',
  Neutral: 'Нормально',
  Good: 'Хорошо',
  VeryGood: 'Отлично',
};

/**
 * Числовые значения MoodLevel из `LifeOS.Domain.Enums.MoodLevel` (1..5).
 *
 * Нужны в двух местах: для графика (ось Y не строит категории) и для
 * сопоставления с полем `predictedMood`, которое AI-сервис возвращает числом.
 */
export const MOOD_SCORES: Record<MoodLevel, 1 | 2 | 3 | 4 | 5> = {
  VeryBad: 1,
  Bad: 2,
  Neutral: 3,
  Good: 4,
  VeryGood: 5,
};

export const MOOD_EMOJI: Record<MoodLevel, string> = {
  VeryBad: '😖',
  Bad: '🙁',
  Neutral: '😐',
  Good: '🙂',
  VeryGood: '😄',
};

/** Обратное преобразование: балл 1..5 → значение перечисления. */
export function moodFromScore(score: number): MoodLevel {
  const clamped = Math.min(5, Math.max(1, Math.round(score)));
  const found = MOOD_VALUES.find((mood) => MOOD_SCORES[mood] === clamped);
  return found ?? 'Neutral';
}

// -------------------------------------------------------------- ModuleType

export type ModuleType = 'General' | 'Study' | 'Finance' | 'Career' | 'Health' | 'Avatar';

export const MODULE_VALUES = [
  'General',
  'Study',
  'Finance',
  'Career',
  'Health',
  'Avatar',
] as const satisfies readonly ModuleType[];

export const MODULE_LABELS: Record<ModuleType, string> = {
  General: 'Общее',
  Study: 'Учёба',
  Finance: 'Финансы',
  Career: 'Карьера',
  Health: 'Здоровье',
  Avatar: 'Аватар',
};
