import type { ZodError } from 'zod';

/**
 * Сообщения об ошибках из результата `safeParse`.
 *
 * zod возвращает размеченное объединение, и разбирать его в каждом ожидании
 * означало бы писать `if (!result.success)` перед любой проверкой. Помощник
 * сводит проверку к сравнению списка сообщений, а для успешного разбора
 * отдаёт пустой список — это же и есть «ошибок нет».
 */
export function messagesOf(result: { success: boolean; error?: ZodError | undefined }): string[] {
  return result.error === undefined ? [] : result.error.issues.map((issue) => issue.message);
}

/** Сообщения, относящиеся к конкретному полю формы. */
export function messagesForField(
  result: { success: boolean; error?: ZodError | undefined },
  field: string,
): string[] {
  if (result.error === undefined) return [];
  return result.error.issues
    .filter((issue) => issue.path[0] === field)
    .map((issue) => issue.message);
}
