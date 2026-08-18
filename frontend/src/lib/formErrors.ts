import type { FieldValues, Path, UseFormSetError } from 'react-hook-form';

import { ApiError } from '@/types/errors';

/**
 * Перенос серверных ошибок валидации в форму.
 *
 * Бэкенд отдаёт ValidationProblemDetails: словарь «поле → сообщения», где имена
 * полей в PascalCase (FluentValidation берёт их из C#-свойств: «Email»,
 * «NewPassword»). Поля формы названы в camelCase, поэтому первая буква
 * приводится к строчной.
 *
 * ЗАЧЕМ ЭТО НУЖНО, если та же валидация уже есть в zod. Серверных правил
 * больше: уникальность email, совпадение текущего пароля, существование
 * указанной цели. Такие проверки невозможны на клиенте — он не видит базу.
 * Без этой функции они превращались бы в безадресное всплывающее сообщение,
 * и пользователь не понимал бы, какое поле исправлять.
 *
 * Возвращает true, если хотя бы одна ошибка привязана к полю. Ответ нужен
 * вызывающему коду, чтобы решить, показывать ли ещё и общее сообщение.
 */
export function applyServerErrors<TFieldValues extends FieldValues>(
  error: unknown,
  setError: UseFormSetError<TFieldValues>,
  knownFields: readonly Path<TFieldValues>[],
): boolean {
  if (!(error instanceof ApiError) || error.fieldErrors === undefined) return false;

  let matched = false;

  for (const [rawKey, messages] of Object.entries(error.fieldErrors)) {
    const message = messages[0];
    if (message === undefined) continue;

    // Ошибки привязки модели приходят с ключами вида «$.date» или «request».
    // Отсекаем префикс и приводим к camelCase.
    const cleaned = rawKey.replace(/^\$\./, '').split('.').pop() ?? rawKey;
    const camelCase = cleaned.charAt(0).toLowerCase() + cleaned.slice(1);

    const field = knownFields.find((known) => known === camelCase);
    if (field !== undefined) {
      setError(field, { type: 'server', message });
      matched = true;
    }
  }

  return matched;
}
